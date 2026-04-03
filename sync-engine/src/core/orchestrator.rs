//! Sync orchestrator - Manages sync jobs and coordinates operations
//!
//! The orchestrator is the central coordinator for all synchronization operations.
//! It manages multiple sync jobs, coordinates between scanner, differ, and transfer
//! components, and handles conflict resolution.

use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;
use tracing::{info, error, debug};

use crate::models::sync_job::{SyncJob, SyncJobConfig, SyncStatus, ConflictResolution};
use crate::models::file_node::FileNode;
use crate::core::scanner::FileScanner;
use crate::core::differ::BlockDiffer;
use crate::core::watcher::FileSystemWatcher;
use crate::storage::database::Database;
use crate::storage::metadata::MetadataStore;
use crate::storage::journal::ChangeJournal;
use crate::network::transfer::TransferEngine;
use super::{SyncError, SyncResult, FileDelta};

/// Sync orchestrator - manages and coordinates sync operations
pub struct SyncOrchestrator {
    jobs: Arc<RwLock<HashMap<String, SyncJob>>>,
    scanner: FileScanner,
    differ: BlockDiffer,
    watcher: FileSystemWatcher,
    database: Database,
    metadata_store: MetadataStore,
    journal: ChangeJournal,
    transfer_engine: TransferEngine,
}

impl SyncOrchestrator {
    /// Create a new sync orchestrator
    pub async fn new() -> SyncResult<Self> {
        info!("Initializing sync orchestrator");

        let database = Database::new("sync_engine.db").await
            .map_err(|e| SyncError::Orchestrator(format!("Failed to initialize database: {}", e)))?;

        let metadata_store = MetadataStore::new(database.clone())
            .map_err(|e| SyncError::Orchestrator(format!("Failed to initialize metadata store: {}", e)))?;

        let journal = ChangeJournal::new(database.clone())
            .map_err(|e| SyncError::Orchestrator(format!("Failed to initialize journal: {}", e)))?;

        let scanner = FileScanner::new(metadata_store.clone());
        let differ = BlockDiffer::new();
        let watcher = FileSystemWatcher::new();
        let transfer_engine = TransferEngine::new();

        Ok(Self {
            jobs: Arc::new(RwLock::new(HashMap::new())),
            scanner,
            differ,
            watcher,
            database,
            metadata_store,
            journal,
            transfer_engine,
        })
    }

    /// Add a new sync job
    pub async fn add_job(&mut self, job: SyncJob) -> SyncResult<()> {
        let job_id = job.id.clone();
        debug!("Adding sync job: {}", job_id);
        
        // Store job in database
        self.database.save_job(&job).await
            .map_err(|e| SyncError::Orchestrator(format!("Failed to save job: {}", e)))?;

        let mut jobs = self.jobs.write().await;
        jobs.insert(job_id.clone(), job);
        
        info!("Added sync job: {}", job_id);
        Ok(())
    }

    /// Get a sync job by ID
    pub async fn get_job(&self, job_id: &str) -> Option<SyncJob> {
        let jobs = self.jobs.read().await;
        jobs.get(job_id).cloned()
    }

    /// List all sync jobs
    pub async fn list_jobs(&self) -> Vec<SyncJob> {
        let jobs = self.jobs.read().await;
        jobs.values().cloned().collect()
    }

    /// Start a sync job
    pub async fn start_job(&mut self, job_id: &str) -> SyncResult<()> {
        info!("Starting sync job: {}", job_id);

        let mut jobs = self.jobs.write().await;
        let job = jobs.get_mut(job_id)
            .ok_or_else(|| SyncError::Orchestrator(format!("Job not found: {}", job_id)))?;

        if job.status != SyncStatus::Idle && job.status != SyncStatus::Paused {
            return Err(SyncError::Orchestrator(format!("Job is not in a startable state: {:?}", job.status)));
        }

        job.status = SyncStatus::Scanning;
        job.start_time = Some(chrono::Utc::now());
        job.error_message = None;

        // Execute sync job synchronously (simplified for testing)
        let job_id_clone = job_id.to_string();
        let jobs_ref = self.jobs.clone();
        let scanner = self.scanner.clone();
        let differ = self.differ.clone();
        let transfer_engine = self.transfer_engine.clone();
        let metadata_store = self.metadata_store.clone();
        let journal = self.journal.clone();

        if let Err(e) = Self::execute_sync_job(
            job_id_clone,
            jobs_ref,
            scanner,
            differ,
            transfer_engine,
            metadata_store,
            journal,
        ).await {
            error!("Sync job {} failed: {}", job_id, e);
            job.error_message = Some(e.to_string());
            job.status = SyncStatus::Failed;
        }

        Ok(())
    }

    /// Stop a sync job
    pub async fn stop_job(&mut self, job_id: &str) -> SyncResult<()> {
        info!("Stopping sync job: {}", job_id);

        let mut jobs = self.jobs.write().await;
        let job = jobs.get_mut(job_id)
            .ok_or_else(|| SyncError::Orchestrator(format!("Job not found: {}", job_id)))?;

        match job.status {
            SyncStatus::Scanning | SyncStatus::Comparing | SyncStatus::Transferring | SyncStatus::ResolvingConflicts => {
                job.status = SyncStatus::Paused;
                info!("Paused sync job: {}", job_id);
                Ok(())
            }
            _ => Err(SyncError::Orchestrator(format!("Cannot stop job in state: {:?}", job.status))),
        }
    }

    /// Resolve a conflict
    pub async fn resolve_conflict(&mut self, job_id: &str, conflict_id: &str, resolution: i32) -> SyncResult<()> {
        info!("Resolving conflict {} for job {}", conflict_id, job_id);

        let mut jobs = self.jobs.write().await;
        let job = jobs.get_mut(job_id)
            .ok_or_else(|| SyncError::Orchestrator(format!("Job not found: {}", job_id)))?;

        let resolution = match resolution {
            0 => ConflictResolution::KeepSource,
            1 => ConflictResolution::KeepTarget,
            2 => ConflictResolution::KeepBoth,
            3 => ConflictResolution::Skip,
            _ => return Err(SyncError::Orchestrator("Invalid conflict resolution".to_string())),
        };

        job.resolve_conflict(conflict_id, resolution)
            .map_err(|e| SyncError::Orchestrator(format!("Failed to resolve conflict: {}", e)))?;

        info!("Resolved conflict {} for job {}", conflict_id, job_id);
        Ok(())
    }

    /// Execute a sync job (internal async task)
    async fn execute_sync_job(
        job_id: String,
        jobs: Arc<RwLock<HashMap<String, SyncJob>>>,
        scanner: FileScanner,
        differ: BlockDiffer,
        transfer_engine: TransferEngine,
        metadata_store: MetadataStore,
        journal: ChangeJournal,
    ) -> SyncResult<()> {
        // Get job config
        let (source_path, target_path, block_size) = {
            let jobs = jobs.read().await;
            let job = jobs.get(&job_id)
                .ok_or_else(|| SyncError::Orchestrator(format!("Job not found: {}", job_id)))?;
            (
                job.config.source_path.clone(),
                job.config.target_path.clone(),
                job.config.block_size,
            )
        };

        // Phase 1: Scan source directory
        info!("Scanning source directory: {}", source_path);
        let source_tree = scanner.scan_directory(&source_path).await
            .map_err(|e| SyncError::Orchestrator(format!("Failed to scan source: {}", e)))?;

        Self::update_job_progress(&jobs, &job_id, |job| {
            job.status = SyncStatus::Comparing;
            job.progress.files_scanned = source_tree.file_count();
        }).await;

        // Phase 2: Scan target directory
        info!("Scanning target directory: {}", target_path);
        let target_tree = scanner.scan_directory(&target_path).await
            .map_err(|e| SyncError::Orchestrator(format!("Failed to scan target: {}", e)))?;

        // Phase 3: Compare and compute deltas
        info!("Computing file deltas");
        let deltas = Self::compute_deltas(&source_tree, &target_tree, &differ, block_size).await?;

        Self::update_job_progress(&jobs, &job_id, |job| {
            job.status = SyncStatus::Transferring;
            job.progress.total_files = deltas.len();
            job.progress.total_bytes = deltas.iter().map(|d| d.total_bytes).sum();
        }).await;

        // Phase 4: Transfer files
        info!("Transferring {} files", deltas.len());
        for delta in deltas {
            transfer_engine.transfer_file(&delta).await
                .map_err(|e| SyncError::Orchestrator(format!("Failed to transfer file: {}", e)))?;

            Self::update_job_progress(&jobs, &job_id, |job| {
                job.progress.files_transferred += 1;
                job.progress.bytes_transferred += delta.total_bytes;
            }).await;
        }

        // Phase 5: Update metadata
        info!("Updating metadata");
        metadata_store.update_tree(&source_tree).await
            .map_err(|e| SyncError::Orchestrator(format!("Failed to update metadata: {}", e)))?;

        // Phase 6: Record changes in journal
        journal.record_sync(&job_id, &source_tree).await
            .map_err(|e| SyncError::Orchestrator(format!("Failed to record changes: {}", e)))?;

        // Mark job as completed
        Self::update_job_progress(&jobs, &job_id, |job| {
            job.status = SyncStatus::Completed;
            job.end_time = Some(chrono::Utc::now());
        }).await;

        info!("Sync job {} completed successfully", job_id);
        Ok(())
    }

    /// Compute deltas between source and target trees
    async fn compute_deltas(
        source_tree: &FileNode,
        target_tree: &FileNode,
        differ: &BlockDiffer,
        block_size: usize,
    ) -> SyncResult<Vec<FileDelta>> {
        let mut deltas = Vec::new();

        // Compare source files with target files
        for source_file in source_tree.flatten_files() {
            if let Some(target_file) = target_tree.find_file(&source_file.path) {
                // File exists in both, compute delta
                if source_file.hash != target_file.hash {
                    let delta = differ.compute_delta(&source_file, &target_file, block_size).await
                        .map_err(|e| SyncError::Differ(format!("Failed to compute delta: {}", e)))?;
                    deltas.push(delta);
                }
            } else {
                // File only in source, need full transfer
                let delta = FileDelta {
                    source_path: source_file.path.clone(),
                    target_path: source_file.path.clone(),
                    blocks: vec![],
                    total_bytes: source_file.size as u64,
                    transfer_percentage: 100.0,
                };
                deltas.push(delta);
            }
        }

        Ok(deltas)
    }

    /// Update job progress with a closure
    async fn update_job_progress<F>(jobs: &Arc<RwLock<HashMap<String, SyncJob>>>, job_id: &str, f: F)
    where
        F: FnOnce(&mut SyncJob),
    {
        let mut jobs = jobs.write().await;
        if let Some(job) = jobs.get_mut(job_id) {
            f(job);
        }
    }
}

impl Clone for SyncOrchestrator {
    fn clone(&self) -> Self {
        Self {
            jobs: self.jobs.clone(),
            scanner: self.scanner.clone(),
            differ: self.differ.clone(),
            watcher: self.watcher.clone(),
            database: self.database.clone(),
            metadata_store: self.metadata_store.clone(),
            journal: self.journal.clone(),
            transfer_engine: self.transfer_engine.clone(),
        }
    }
}
