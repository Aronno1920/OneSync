//! Metadata store - Manages file metadata storage and retrieval
//!
//! This module provides efficient storage and retrieval of file metadata
//! for tracking changes and enabling fast comparisons.

use std::collections::HashMap;
use std::sync::Arc;
use tracing::{debug, info};
use rusqlite::params;

use crate::models::file_node::FileNode;
use crate::storage::database::Database;
use super::{StorageError, StorageResult};

/// Metadata store for file metadata management
#[derive(Clone)]
pub struct MetadataStore {
    database: Database,
    cache: Arc<tokio::sync::RwLock<HashMap<String, FileNode>>>,
}

impl MetadataStore {
    /// Create a new metadata store
    pub fn new(database: Database) -> StorageResult<Self> {
        info!("Initializing metadata store");

        Ok(Self {
            database,
            cache: Arc::new(tokio::sync::RwLock::new(HashMap::new())),
        })
    }

    /// Update a file tree in the store
    pub async fn update_tree(&self, root: &FileNode) -> StorageResult<()> {
        debug!("Updating file tree: {}", root.path);

        // Update all files in the tree
        for file in root.flatten_files() {
            self.update_file(&file, "default_job").await?;
        }

        info!("Updated {} files in metadata store", root.file_count());
        Ok(())
    }

    /// Update a single file in the store
    pub async fn update_file(&self, file: &FileNode, job_id: &str) -> StorageResult<()> {
        debug!("Updating file metadata: {}", file.path);

        // Update database
        self.database.save_file_metadata(file, job_id).await?;

        // Update cache
        let mut cache = self.cache.write().await;
        cache.insert(file.path.clone(), file.clone());

        Ok(())
    }

    /// Get file metadata from the store
    pub async fn get_file(&self, path: &str) -> StorageResult<Option<FileNode>> {
        debug!("Getting file metadata: {}", path);

        // Check cache first
        {
            let cache = self.cache.read().await;
            if let Some(file) = cache.get(path) {
                return Ok(Some(file.clone()));
            }
        }

        // Load from database
        match self.database.load_file_metadata(path).await {
            Ok(file) => {
                // Update cache
                let mut cache = self.cache.write().await;
                cache.insert(path.to_string(), file.clone());
                Ok(Some(file))
            }
            Err(StorageError::NotFound(_)) => Ok(None),
            Err(e) => Err(e),
        }
    }

    /// Get all files for a job
    pub async fn get_files_for_job(&self, job_id: &str) -> StorageResult<Vec<FileNode>> {
        debug!("Getting files for job: {}", job_id);

        let conn = self.database.conn.lock().unwrap();

        let mut stmt = conn.prepare(
            "SELECT path, is_directory, size, modified_time, hash
             FROM file_metadata WHERE job_id = ?1"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let files = stmt.query_map(params![job_id], |row| {
            Ok(FileNode {
                path: row.get(0)?,
                is_directory: row.get(1)?,
                size: row.get(2)?,
                modified_time: row.get(3)?,
                hash: row.get(4)?,
                children: Vec::new(),
            })
        }).map_err(|e| StorageError::Database(format!("Failed to query files: {}", e)))?
            .collect::<Result<Vec<_>, _>>()
            .map_err(|e| StorageError::Database(format!("Failed to collect files: {}", e)))?;

        Ok(files)
    }

    /// Delete file metadata
    pub async fn delete_file(&self, path: &str) -> StorageResult<()> {
        debug!("Deleting file metadata: {}", path);

        let conn = self.database.conn.lock().unwrap();

        conn.execute("DELETE FROM file_metadata WHERE path = ?1", params![path])
            .map_err(|e| StorageError::Database(format!("Failed to delete file metadata: {}", e)))?;

        // Remove from cache
        let mut cache = self.cache.write().await;
        cache.remove(path);

        Ok(())
    }

    /// Clear all metadata for a job
    pub async fn clear_job(&self, job_id: &str) -> StorageResult<()> {
        debug!("Clearing metadata for job: {}", job_id);

        let conn = self.database.conn.lock().unwrap();

        conn.execute("DELETE FROM file_metadata WHERE job_id = ?1", params![job_id])
            .map_err(|e| StorageError::Database(format!("Failed to clear job metadata: {}", e)))?;

        // Clear cache for this job
        let files = self.get_files_for_job(job_id).await?;
        let mut cache = self.cache.write().await;
        for file in files {
            cache.remove(&file.path);
        }

        Ok(())
    }

    /// Get cache statistics
    pub async fn cache_stats(&self) -> (usize, usize) {
        let cache = self.cache.read().await;
        (cache.len(), cache.capacity())
    }

    /// Clear the metadata cache
    pub async fn clear_cache(&self) {
        let mut cache = self.cache.write().await;
        cache.clear();
    }
}
