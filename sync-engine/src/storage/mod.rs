//! Storage module - Database and metadata management
//!
//! This module handles persistent storage of sync metadata, file information,
//! and change journals using SQLite.

pub mod database;
pub mod metadata;
pub mod journal;

pub use database::Database;
pub use metadata::MetadataStore;
pub use journal::ChangeJournal;

/// Storage error types
#[derive(Debug, thiserror::Error)]
pub enum StorageError {
    #[error("Database error: {0}")]
    Database(String),

    #[error("Serialization error: {0}")]
    Serialization(String),

    #[error("Not found: {0}")]
    NotFound(String),

    #[error("Invalid data: {0}")]
    InvalidData(String),
}

/// Result type for storage operations
pub type StorageResult<T> = std::result::Result<T, StorageError>;
