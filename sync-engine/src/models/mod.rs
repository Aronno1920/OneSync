//! Data models for the sync engine
//!
//! This module contains the core data structures used throughout the sync engine,
//! including file nodes, sync jobs, and related types.

pub mod file_node;
pub mod sync_job;

pub use file_node::FileNode;
pub use sync_job::{SyncJob, SyncJobConfig, SyncDirection, SyncStatus, ConflictStrategy, ConflictResolution, SyncProgress, ConflictInfo};
