//! File system watcher - Monitors file system changes
//!
//! The file system watcher uses platform-native APIs (inotify on Linux,
//! FSEvents on macOS, ReadDirectoryChangesW on Windows) to efficiently
//! monitor file system changes in real-time.

use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::time::SystemTime;
use tokio::sync::{mpsc, RwLock};
use tracing::{debug, info, warn};
use notify::{RecommendedWatcher, RecursiveMode, Watcher, Event, EventKind, Result as NotifyResult};
use uuid::Uuid;

use crate::storage::journal::ChangeJournal;
use super::{SyncError, SyncResult};

/// File system event types
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub enum FsEvent {
    /// File or directory created
    Created { path: String },
    /// File or directory removed
    Removed { path: String },
    /// File or directory renamed
    Renamed { old_path: String, new_path: String },
    /// File modified
    Modified { path: String },
    /// Metadata changed
    Metadata { path: String },
}

/// File system watcher configuration
#[derive(Debug, Clone)]
pub struct WatcherConfig {
    /// Enable recursive watching
    pub recursive: bool,
    /// Debounce interval in milliseconds
    pub debounce_ms: u64,
    /// Buffer size for events
    pub buffer_size: usize,
}

impl Default for WatcherConfig {
    fn default() -> Self {
        Self {
            recursive: true,
            debounce_ms: 100,
            buffer_size: 1024,
        }
    }
}

/// File system watcher - monitors directories for changes
#[derive(Clone)]
pub struct FileSystemWatcher {
    config: WatcherConfig,
    watchers: Arc<RwLock<HashMap<String, RecommendedWatcher>>>,
    event_tx: Arc<mpsc::UnboundedSender<FsEvent>>,
    event_rx: Arc<RwLock<Option<mpsc::UnboundedReceiver<FsEvent>>>>,
}

impl FileSystemWatcher {
    /// Create a new file system watcher
    pub fn new() -> Self {
        let (event_tx, event_rx) = mpsc::unbounded_channel();

        Self {
            config: WatcherConfig::default(),
            watchers: Arc::new(RwLock::new(HashMap::new())),
            event_tx: Arc::new(event_tx),
            event_rx: Arc::new(RwLock::new(Some(event_rx))),
        }
    }

    /// Create a new file system watcher with custom config
    pub fn with_config(config: WatcherConfig) -> Self {
        let (event_tx, event_rx) = mpsc::unbounded_channel();

        Self {
            config,
            watchers: Arc::new(RwLock::new(HashMap::new())),
            event_tx: Arc::new(event_tx),
            event_rx: Arc::new(RwLock::new(Some(event_rx))),
        }
    }

    /// Watch a directory for changes
    pub async fn watch(&self, path: &str) -> SyncResult<()> {
        let path = PathBuf::from(path);
        if !path.exists() {
            return Err(SyncError::FileNotFound(format!("Path not found: {}", path.display())));
        }

        let path_str = path.to_string_lossy().to_string();
        debug!("Watching directory: {}", path_str);

        let event_tx = self.event_tx.clone();
        let mut watchers = self.watchers.write().await;

        // Create watcher
        let mut watcher = notify::recommended_watcher(move |res: NotifyResult<Event>| {
            match res {
                Ok(event) => {
                    debug!("File system event: {:?}", event);
                    Self::handle_event(&event, &event_tx);
                }
                Err(e) => {
                    warn!("Watch error: {:?}", e);
                }
            }
        }).map_err(|e| SyncError::Watcher(format!("Failed to create watcher: {}", e)))?;

        // Add watch
        let mode = if self.config.recursive {
            RecursiveMode::Recursive
        } else {
            RecursiveMode::NonRecursive
        };

        watcher.watch(&path, mode)
            .map_err(|e| SyncError::Watcher(format!("Failed to watch path: {}", e)))?;

        watchers.insert(path_str.clone(), watcher);
        info!("Started watching: {}", path_str);

        Ok(())
    }

    /// Unwatch a directory
    pub async fn unwatch(&self, path: &str) -> SyncResult<()> {
        let path_str = path.to_string();
        debug!("Unwatching directory: {}", path_str);

        let mut watchers = self.watchers.write().await;
        if watchers.remove(&path_str).is_some() {
            info!("Stopped watching: {}", path_str);
            Ok(())
        } else {
            Err(SyncError::Watcher(format!("Not watching: {}", path_str)))
        }
    }

    /// Get a receiver for file system events
    pub async fn events(&self) -> mpsc::UnboundedReceiver<FsEvent> {
        let mut event_rx = self.event_rx.write().await;
        event_rx.take().expect("Event receiver already taken")
    }

    /// Handle a notify event
    fn handle_event(event: &Event, event_tx: &mpsc::UnboundedSender<FsEvent>) {
        for path in &event.paths {
            let path_str = path.to_string_lossy().to_string();

            let fs_event = match event.kind {
                EventKind::Create(_) => {
                    Some(FsEvent::Created { path: path_str })
                }
                EventKind::Remove(_) => {
                    Some(FsEvent::Removed { path: path_str })
                }
                EventKind::Modify(_) => {
                    Some(FsEvent::Modified { path: path_str })
                }
                _ => None,
            };

            if let Some(fs_event) = fs_event {
                if let Err(e) = event_tx.send(fs_event) {
                    warn!("Failed to send event: {}", e);
                }
            }
        }
    }

    /// Process events and update journal
    pub async fn process_events(&self, journal: &ChangeJournal) -> SyncResult<()> {
        let mut event_rx = self.events().await;

        while let Some(event) = event_rx.recv().await {
            debug!("Processing event: {:?}", event);

            match event {
                FsEvent::Created { path } => {
                    journal.record_change(&path, "created").await
                        .map_err(|e| SyncError::Watcher(format!("Failed to record change: {}", e)))?;
                }
                FsEvent::Removed { path } => {
                    journal.record_change(&path, "removed").await
                        .map_err(|e| SyncError::Watcher(format!("Failed to record change: {}", e)))?;
                }
                FsEvent::Modified { path } => {
                    journal.record_change(&path, "modified").await
                        .map_err(|e| SyncError::Watcher(format!("Failed to record change: {}", e)))?;
                }
                FsEvent::Renamed { old_path, new_path } => {
                    journal.record_change(&old_path, "renamed_from").await
                        .map_err(|e| SyncError::Watcher(format!("Failed to record change: {}", e)))?;
                    journal.record_change(&new_path, "renamed_to").await
                        .map_err(|e| SyncError::Watcher(format!("Failed to record change: {}", e)))?;
                }
                FsEvent::Metadata { path } => {
                    journal.record_change(&path, "metadata_changed").await
                        .map_err(|e| SyncError::Watcher(format!("Failed to record change: {}", e)))?;
                }
            }
        }

        Ok(())
    }
}

impl Default for FileSystemWatcher {
    fn default() -> Self {
        Self::new()
    }
}
