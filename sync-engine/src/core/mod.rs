//! Core synchronization engine module
//!
//! This module contains the core synchronization logic including:
//! - Sync orchestrator: Manages sync jobs and coordinates operations
//! - File scanner: Scans directories and builds file trees
//! - Block differ: Computes delta differences between files
//! - File system watcher: Monitors file system changes

pub mod orchestrator;
pub mod scanner;
pub mod differ;
pub mod watcher;

pub use orchestrator::SyncOrchestrator;
pub use scanner::FileScanner;
pub use differ::BlockDiffer;
pub use watcher::FileSystemWatcher;

/// Core synchronization error types
#[derive(Debug, thiserror::Error)]
pub enum SyncError {
    #[error("Scanner error: {0}")]
    Scanner(String),

    #[error("Differ error: {0}")]
    Differ(String),

    #[error("Watcher error: {0}")]
    Watcher(String),

    #[error("Orchestrator error: {0}")]
    Orchestrator(String),

    #[error("File not found: {0}")]
    FileNotFound(String),

    #[error("Permission denied: {0}")]
    PermissionDenied(String),

    #[error("Invalid file: {0}")]
    InvalidFile(String),
}

/// Result type for core operations
pub type SyncResult<T> = std::result::Result<T, SyncError>;

/// Block information for delta transfer
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct BlockInfo {
    /// Block index
    pub index: u64,
    /// Block offset in file
    pub offset: u64,
    /// Block size
    pub size: usize,
    /// Block hash
    pub hash: [u8; 32],
}

/// Delta information for a file
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct FileDelta {
    /// Source file path
    pub source_path: String,
    /// Target file path
    pub target_path: String,
    /// Blocks to transfer
    pub blocks: Vec<BlockInfo>,
    /// Total bytes to transfer
    pub total_bytes: u64,
    /// Percentage of file that needs transfer
    pub transfer_percentage: f64,
}
