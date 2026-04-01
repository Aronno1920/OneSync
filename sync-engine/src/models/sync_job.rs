//! Sync job model - Represents a synchronization job
//!
//! This module provides the SyncJob structure which represents a synchronization
//! job with its configuration, status, and progress information.

use std::collections::HashMap;
use serde::{Serialize, Deserialize};
use chrono::{DateTime, Utc};
use uuid::Uuid;

/// Sync direction
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SyncDirection {
    /// Bidirectional sync
    Bidirectional = 0,
    /// Source to target only
    SourceToTarget = 1,
    /// Target to source only
    TargetToSource = 2,
}

/// Conflict resolution strategy
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ConflictStrategy {
    /// Last write wins
    LastWriteWins = 0,
    /// Manual resolution required
    Manual = 1,
    /// Skip conflicting files
    Skip = 2,
}

/// Conflict resolution choice
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ConflictResolution {
    /// Keep source version
    KeepSource = 0,
    /// Keep target version
    KeepTarget = 1,
    /// Keep both versions (rename)
    KeepBoth = 2,
    /// Skip this file
    Skip = 3,
}

/// Sync job status
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum SyncStatus {
    /// Job is idle
    Idle = 0,
    /// Scanning files
    Scanning = 1,
    /// Comparing files
    Comparing = 2,
    /// Transferring files
    Transferring = 3,
    /// Resolving conflicts
    ResolvingConflicts = 4,
    /// Job completed successfully
    Completed = 5,
    /// Job failed
    Failed = 6,
    /// Job paused
    Paused = 7,
}

/// Sync job configuration
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SyncJobConfig {
    /// Source directory path
    pub source_path: String,
    /// Target directory path
    pub target_path: String,
    /// Sync direction
    pub direction: SyncDirection,
    /// Conflict resolution strategy
    pub conflict_strategy: ConflictStrategy,
    /// Exclude patterns (glob patterns)
    pub exclude_patterns: Vec<String>,
    /// Include patterns (glob patterns)
    pub include_patterns: Vec<String>,
    /// Enable compression
    pub enable_compression: bool,
    /// Block size for delta transfer
    pub block_size: usize,
}

impl Default for SyncJobConfig {
    fn default() -> Self {
        Self {
            source_path: String::new(),
            target_path: String::new(),
            direction: SyncDirection::Bidirectional,
            conflict_strategy: ConflictStrategy::LastWriteWins,
            exclude_patterns: Vec::new(),
            include_patterns: Vec::new(),
            enable_compression: true,
            block_size: 256 * 1024, // 256KB
        }
    }
}

/// Sync progress information
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SyncProgress {
    /// Number of files scanned
    pub files_scanned: usize,
    /// Number of files transferred
    pub files_transferred: usize,
    /// Number of bytes transferred
    pub bytes_transferred: u64,
    /// Total number of files to transfer
    pub total_files: usize,
    /// Total number of bytes to transfer
    pub total_bytes: u64,
}

impl Default for SyncProgress {
    fn default() -> Self {
        Self {
            files_scanned: 0,
            files_transferred: 0,
            bytes_transferred: 0,
            total_files: 0,
            total_bytes: 0,
        }
    }
}

impl SyncProgress {
    /// Calculate completion percentage
    pub fn percentage(&self) -> f64 {
        if self.total_bytes == 0 {
            0.0
        } else {
            (self.bytes_transferred as f64 / self.total_bytes as f64) * 100.0
        }
    }
}

/// Conflict information
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ConflictInfo {
    /// Unique conflict ID
    pub conflict_id: String,
    /// File path
    pub file_path: String,
    /// Source version
    pub source_version: FileVersion,
    /// Target version
    pub target_version: FileVersion,
    /// Conflict reason
    pub reason: String,
    /// Resolution status
    pub resolved: bool,
    /// Resolution choice
    pub resolution: Option<ConflictResolution>,
}

/// File version information
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileVersion {
    /// File path
    pub path: String,
    /// File size
    pub size: u64,
    /// Modified time
    pub modified_time: i64,
    /// File hash
    pub hash: String,
}

/// Sync job - represents a synchronization job
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SyncJob {
    /// Unique job ID
    pub id: String,
    /// Job configuration
    pub config: SyncJobConfig,
    /// Job status
    pub status: SyncStatus,
    /// Creation time
    pub created_at: DateTime<Utc>,
    /// Start time
    pub start_time: Option<DateTime<Utc>>,
    /// End time
    pub end_time: Option<DateTime<Utc>>,
    /// Error message (if failed)
    pub error_message: Option<String>,
    /// Progress information
    pub progress: SyncProgress,
    /// Conflicts
    pub conflicts: Vec<ConflictInfo>,
}

impl SyncJob {
    /// Create a new sync job
    pub fn new(id: String, config: SyncJobConfig) -> Self {
        Self {
            id,
            config,
            status: SyncStatus::Idle,
            created_at: Utc::now(),
            start_time: None,
            end_time: None,
            error_message: None,
            progress: SyncProgress::default(),
            conflicts: Vec::new(),
        }
    }

    /// Create a new sync job with generated ID
    pub fn with_config(config: SyncJobConfig) -> Self {
        Self::new(Uuid::new_v4().to_string(), config)
    }

    /// Add a conflict to the job
    pub fn add_conflict(&mut self, conflict: ConflictInfo) {
        self.conflicts.push(conflict);
    }

    /// Resolve a conflict
    pub fn resolve_conflict(&mut self, conflict_id: &str, resolution: ConflictResolution) -> Result<(), String> {
        let conflict = self.conflicts.iter_mut()
            .find(|c| c.conflict_id == conflict_id)
            .ok_or_else(|| format!("Conflict not found: {}", conflict_id))?;

        conflict.resolved = true;
        conflict.resolution = Some(resolution);
        Ok(())
    }

    /// Get unresolved conflicts
    pub fn unresolved_conflicts(&self) -> Vec<&ConflictInfo> {
        self.conflicts.iter()
            .filter(|c| !c.resolved)
            .collect()
    }

    /// Check if job has conflicts
    pub fn has_conflicts(&self) -> bool {
        !self.conflicts.is_empty()
    }

    /// Check if job has unresolved conflicts
    pub fn has_unresolved_conflicts(&self) -> bool {
        self.conflicts.iter().any(|c| !c.resolved)
    }

    /// Get job duration in seconds
    pub fn duration(&self) -> Option<i64> {
        match (self.start_time, self.end_time) {
            (Some(start), Some(end)) => Some((end - start).num_seconds()),
            (Some(start), None) => Some((Utc::now() - start).num_seconds()),
            _ => None,
        }
    }

    /// Check if job is running
    pub fn is_running(&self) -> bool {
        matches!(self.status, SyncStatus::Scanning | SyncStatus::Comparing | SyncStatus::Transferring | SyncStatus::ResolvingConflicts)
    }

    /// Check if job is completed
    pub fn is_completed(&self) -> bool {
        self.status == SyncStatus::Completed || self.status == SyncStatus::Failed
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_sync_job_creation() {
        let config = SyncJobConfig::default();
        let job = SyncJob::with_config(config);
        assert_eq!(job.status, SyncStatus::Idle);
        assert!(!job.is_running());
    }

    #[test]
    fn test_conflict_resolution() {
        let config = SyncJobConfig::default();
        let mut job = SyncJob::with_config(config);

        let conflict = ConflictInfo {
            conflict_id: "test_conflict".to_string(),
            file_path: "/test/file.txt".to_string(),
            source_version: FileVersion {
                path: "/test/file.txt".to_string(),
                size: 100,
                modified_time: 1000,
                hash: "hash1".to_string(),
            },
            target_version: FileVersion {
                path: "/test/file.txt".to_string(),
                size: 200,
                modified_time: 2000,
                hash: "hash2".to_string(),
            },
            reason: "Both modified".to_string(),
            resolved: false,
            resolution: None,
        };

        job.add_conflict(conflict);
        assert!(job.has_conflicts());
        assert!(job.has_unresolved_conflicts());

        job.resolve_conflict("test_conflict", ConflictResolution::KeepSource).unwrap();
        assert!(!job.has_unresolved_conflicts());
    }

    #[test]
    fn test_progress_percentage() {
        let mut progress = SyncProgress::default();
        progress.total_bytes = 1000;
        progress.bytes_transferred = 500;
        assert_eq!(progress.percentage(), 50.0);
    }
}
