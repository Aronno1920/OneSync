//! OneSync Engine - High-performance file synchronization engine
//!
//! This library provides the core synchronization engine for OneSync, including:
//! - File system scanning and change detection
//! - Block-level delta transfer using rsync-like algorithms
//! - Conflict resolution strategies
//! - Metadata storage and journaling
//! - Network transfer protocols
//! - IPC layer for communication with the UI

pub mod ipc;
pub mod core;
pub mod storage;
pub mod network;
pub mod models;

// Re-export commonly used types
pub use models::file_node::FileNode;
pub use models::sync_job::{SyncJob, SyncJobConfig, SyncDirection, SyncStatus};
pub use core::orchestrator::SyncOrchestrator;
pub use core::scanner::FileScanner;
pub use core::differ::BlockDiffer;
pub use core::watcher::FileSystemWatcher;
pub use storage::database::Database;
pub use storage::metadata::MetadataStore;
pub use storage::journal::ChangeJournal;
pub use network::transfer::TransferEngine;
pub use network::rsync_algorithm::RsyncAlgorithm;

/// Library version
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// Default block size for delta transfer (256KB)
pub const DEFAULT_BLOCK_SIZE: usize = 256 * 1024;

/// Maximum concurrent file operations
pub const MAX_CONCURRENT_OPS: usize = 16;

/// Result type alias for the library
pub type Result<T> = std::result::Result<T, Error>;

/// Error type for the sync engine
#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Database error: {0}")]
    Database(#[from] rusqlite::Error),

    #[error("Serialization error: {0}")]
    Serialization(#[from] bincode::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),

    #[error("Network error: {0}")]
    Network(String),

    #[error("Sync error: {0}")]
    Sync(String),

    #[error("Conflict detected: {0}")]
    Conflict(String),

    #[error("Invalid configuration: {0}")]
    Config(String),

    #[error("Not found: {0}")]
    NotFound(String),
}

// FFI exports for C# interop
#[cfg(feature = "ffi")]
pub mod ffi {
    use super::*;
    use std::ffi::{CString, CStr};
    use std::os::raw::{c_char, c_int};
    use std::ptr;

    /// Create a new sync orchestrator
    #[no_mangle]
    pub extern "C" fn sync_engine_create() -> *mut SyncOrchestrator {
        // This is a placeholder - actual implementation would need async runtime
        ptr::null_mut()
    }

    /// Destroy a sync orchestrator
    #[no_mangle]
    pub extern "C" fn sync_engine_destroy(_orchestrator: *mut SyncOrchestrator) {
        // Placeholder implementation
    }

    /// Start a sync job
    #[no_mangle]
    pub extern "C" fn sync_engine_start_job(
        _orchestrator: *mut SyncOrchestrator,
        _job_id: *const c_char,
    ) -> c_int {
        // Placeholder implementation
        0
    }

    /// Get last error message
    #[no_mangle]
    pub extern "C" fn sync_engine_last_error() -> *const c_char {
        // Placeholder implementation
        ptr::null()
    }

    /// Free error message string
    #[no_mangle]
    pub extern "C" fn sync_engine_free_string(_s: *mut c_char) {
        // Placeholder implementation
    }
}
