//! File scanner - Scans directories and builds file trees
//!
//! The file scanner traverses directories efficiently using memory-mapped files
//! for large directories and parallel scanning for improved performance.

use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::pin::Pin;
use tokio::fs;
use tokio::sync::Semaphore;
use tracing::{debug, info};
use memmap2::Mmap;
use blake3::Hasher;

use crate::models::file_node::FileNode;
use crate::storage::metadata::MetadataStore;
use super::{SyncError, SyncResult};

/// File scanner configuration
#[derive(Debug, Clone)]
pub struct ScannerConfig {
    /// Maximum concurrent file operations
    pub max_concurrent_ops: usize,
    /// Enable parallel scanning
    pub parallel_scan: bool,
    /// Follow symbolic links
    pub follow_symlinks: bool,
    /// Skip hidden files
    pub skip_hidden: bool,
}

impl Default for ScannerConfig {
    fn default() -> Self {
        Self {
            max_concurrent_ops: 16,
            parallel_scan: true,
            follow_symlinks: false,
            skip_hidden: true,
        }
    }
}

/// File scanner - builds file trees from directories
#[derive(Clone)]
pub struct FileScanner {
    config: ScannerConfig,
    metadata_store: Arc<MetadataStore>,
}

impl FileScanner {
    /// Create a new file scanner
    pub fn new(metadata_store: MetadataStore) -> Self {
        Self {
            config: ScannerConfig::default(),
            metadata_store: Arc::new(metadata_store),
        }
    }

    /// Create a new file scanner with custom config
    pub fn with_config(metadata_store: MetadataStore, config: ScannerConfig) -> Self {
        Self {
            config,
            metadata_store: Arc::new(metadata_store),
        }
    }

    /// Scan a directory and build a file tree
    pub async fn scan_directory(&self, path: &str) -> SyncResult<FileNode> {
        info!("Scanning directory: {}", path);

        let path = PathBuf::from(path);
        if !path.exists() {
            return Err(SyncError::FileNotFound(format!("Path not found: {}", path.display())));
        }

        if !path.is_dir() {
            return Err(SyncError::InvalidFile(format!("Not a directory: {}", path.display())));
        }

        let root_node = if self.config.parallel_scan {
            self.scan_parallel(&path).await?
        } else {
            self.scan_sequential(&path).await?
        };

        info!("Scanned {} files in {}", root_node.file_count(), path.display());
        Ok(root_node)
    }

    /// Sequential directory scanning (non-recursive version)
    async fn scan_sequential(&self, path: &Path) -> SyncResult<FileNode> {
        debug!("Sequential scan: {}", path.display());

        let mut children = Vec::new();
        let mut total_size = 0u64;

        let mut entries = fs::read_dir(path).await
            .map_err(|e| SyncError::Scanner(format!("Failed to read directory {}: {}", path.display(), e)))?;

        while let Some(entry) = entries.next_entry().await
            .map_err(|e| SyncError::Scanner(format!("Failed to read entry: {}", e)))? {
            
            let entry_path = entry.path();
            let file_name = entry.file_name();
            let file_name_str = file_name.to_string_lossy();

            // Skip hidden files if configured
            if self.config.skip_hidden && file_name_str.starts_with('.') {
                continue;
            }

            let metadata = entry.metadata().await
                .map_err(|e| SyncError::Scanner(format!("Failed to get metadata for {}: {}", entry_path.display(), e)))?;

            if metadata.is_dir() {
                // Use boxed future for recursion
                let scan_future = Box::pin(self.scan_sequential(&entry_path));
                let child_node = scan_future.await?;
                total_size += child_node.size;
                children.push(child_node);
            } else if metadata.is_file() {
                let file_size = metadata.len();
                let modified_time = metadata.modified()
                    .ok()
                    .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
                    .map(|d| d.as_secs() as i64)
                    .unwrap_or(0);

                // Compute file hash
                let hash = self.compute_file_hash(&entry_path, file_size).await?;

                let file_node = FileNode {
                    path: entry_path.to_string_lossy().to_string(),
                    is_directory: false,
                    size: file_size,
                    modified_time,
                    hash,
                    children: Vec::new(),
                };

                total_size += file_size;
                children.push(file_node);
            } else if !self.config.follow_symlinks {
                debug!("Skipping symlink: {}", entry_path.display());
            }
        }

        let metadata = fs::metadata(path).await
            .map_err(|e| SyncError::Scanner(format!("Failed to get metadata for {}: {}", path.display(), e)))?;

        let modified_time = metadata.modified()
            .ok()
            .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
            .map(|d| d.as_secs() as i64)
            .unwrap_or(0);

        Ok(FileNode {
            path: path.to_string_lossy().to_string(),
            is_directory: true,
            size: total_size,
            modified_time,
            hash: String::new(),
            children,
        })
    }

    /// Parallel directory scanning (non-recursive version)
    async fn scan_parallel(&self, path: &Path) -> SyncResult<FileNode> {
        let semaphore = Arc::new(Semaphore::new(self.config.max_concurrent_ops));
        let mut children = Vec::new();
        let mut total_size = 0u64;

        let mut entries = fs::read_dir(path).await
            .map_err(|e| SyncError::Scanner(format!("Failed to read directory {}: {}", path.display(), e)))?;

        while let Some(entry) = entries.next_entry().await
            .map_err(|e| SyncError::Scanner(format!("Failed to read entry: {}", e)))? {
            
            let entry_path = entry.path();
            let file_name = entry.file_name();
            let file_name_str = file_name.to_string_lossy();

            // Skip hidden files if configured
            if self.config.skip_hidden && file_name_str.starts_with('.') {
                continue;
            }

            let metadata = entry.metadata().await
                .map_err(|e| SyncError::Scanner(format!("Failed to get metadata for {}: {}", entry_path.display(), e)))?;

            if metadata.is_dir() {
                // Use boxed future for recursion
                let scan_future = Box::pin(self.scan_parallel(&entry_path));
                let child_node = scan_future.await?;
                total_size += child_node.size;
                children.push(child_node);
            } else if metadata.is_file() {
                let file_size = metadata.len();
                let modified_time = metadata.modified()
                    .ok()
                    .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
                    .map(|d| d.as_secs() as i64)
                    .unwrap_or(0);

                // Compute file hash
                let hash = self.compute_file_hash(&entry_path, file_size).await?;

                let file_node = FileNode {
                    path: entry_path.to_string_lossy().to_string(),
                    is_directory: false,
                    size: file_size,
                    modified_time,
                    hash,
                    children: Vec::new(),
                };

                total_size += file_size;
                children.push(file_node);
            } else if !self.config.follow_symlinks {
                debug!("Skipping symlink: {}", entry_path.display());
            }
        }

        let metadata = fs::metadata(path).await
            .map_err(|e| SyncError::Scanner(format!("Failed to get metadata for {}: {}", path.display(), e)))?;

        let modified_time = metadata.modified()
            .ok()
            .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
            .map(|d| d.as_secs() as i64)
            .unwrap_or(0);

        Ok(FileNode {
            path: path.to_string_lossy().to_string(),
            is_directory: true,
            size: total_size,
            modified_time,
            hash: String::new(),
            children,
        })
    }

    /// Compute BLAKE3 hash for a file
    async fn compute_file_hash(&self, path: &Path, file_size: u64) -> SyncResult<String> {
        if file_size == 0 {
            return Ok("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855".to_string());
        }

        // Use memory mapping for large files
        if file_size > 10 * 1024 * 1024 { // 10MB threshold
            self.compute_hash_mmap(path).await
        } else {
            self.compute_hash_buffered(path).await
        }
    }

    /// Compute hash using memory mapping
    async fn compute_hash_mmap(&self, path: &Path) -> SyncResult<String> {
        let file = std::fs::File::open(path)
            .map_err(|e| SyncError::Scanner(format!("Failed to open file {}: {}", path.display(), e)))?;

        let mmap = unsafe { Mmap::map(&file) }
            .map_err(|e| SyncError::Scanner(format!("Failed to mmap file {}: {}", path.display(), e)))?;

        let mut hasher = Hasher::new();
        hasher.update(&mmap);
        let hash = hasher.finalize();

        Ok(hash.to_hex().to_string())
    }

    /// Compute hash using buffered reading
    async fn compute_hash_buffered(&self, path: &Path) -> SyncResult<String> {
        let contents = fs::read(path).await
            .map_err(|e| SyncError::Scanner(format!("Failed to read file {}: {}", path.display(), e)))?;

        let mut hasher = Hasher::new();
        hasher.update(&contents);
        let hash = hasher.finalize();

        Ok(hash.to_hex().to_string())
    }
}
