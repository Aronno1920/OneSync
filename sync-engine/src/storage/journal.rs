//! Change journal - Records file system changes
//!
//! This module provides a journal for tracking file system changes over time,
//! enabling efficient synchronization and conflict detection.

use std::sync::Arc;
use tracing::{debug, info};
use chrono::Utc;
use rusqlite::params;

use crate::models::file_node::FileNode;
use crate::storage::database::Database;
use super::{StorageError, StorageResult};

/// Change journal entry
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct JournalEntry {
    /// Entry ID
    pub id: i64,
    /// Job ID
    pub job_id: String,
    /// File path
    pub path: String,
    /// Change type (created, removed, modified, etc.)
    pub change_type: String,
    /// Timestamp
    pub timestamp: i64,
}

/// Change journal for tracking file system changes
#[derive(Clone)]
pub struct ChangeJournal {
    database: Database,
}

impl ChangeJournal {
    /// Create a new change journal
    pub fn new(database: Database) -> StorageResult<Self> {
        info!("Initializing change journal");

        Ok(Self { database })
    }

    /// Record a change in the journal
    pub async fn record_change(&self, path: &str, change_type: &str) -> StorageResult<()> {
        debug!("Recording change: {} - {}", change_type, path);

        let conn = self.database.conn.lock().unwrap();

        conn.execute(
            "INSERT INTO change_journal (job_id, path, change_type, timestamp)
             VALUES (?1, ?2, ?3, ?4)",
            params!["default_job", path, change_type, Utc::now().timestamp()],
        ).map_err(|e| StorageError::Database(format!("Failed to record change: {}", e)))?;

        Ok(())
    }

    /// Record a sync operation in the journal
    pub async fn record_sync(&self, job_id: &str, tree: &FileNode) -> StorageResult<()> {
        debug!("Recording sync for job: {}", job_id);

        let conn = self.database.conn.lock().unwrap();

        // Record all files in the tree
        for file in tree.flatten_files() {
            conn.execute(
                "INSERT INTO change_journal (job_id, path, change_type, timestamp)
                 VALUES (?1, ?2, ?3, ?4)",
                params![job_id, file.path, "synced", Utc::now().timestamp()],
            ).map_err(|e| StorageError::Database(format!("Failed to record sync: {}", e)))?;
        }

        info!("Recorded sync for {} files", tree.file_count());
        Ok(())
    }

    /// Get changes for a job
    pub async fn get_changes(&self, job_id: &str) -> StorageResult<Vec<JournalEntry>> {
        debug!("Getting changes for job: {}", job_id);

        let conn = self.database.conn.lock().unwrap();

        let mut stmt = conn.prepare(
            "SELECT id, job_id, path, change_type, timestamp
             FROM change_journal WHERE job_id = ?1 ORDER BY timestamp DESC"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let entries = stmt.query_map(params![job_id], |row| {
            Ok(JournalEntry {
                id: row.get(0)?,
                job_id: row.get(1)?,
                path: row.get(2)?,
                change_type: row.get(3)?,
                timestamp: row.get(4)?,
            })
        }).map_err(|e| StorageError::Database(format!("Failed to query changes: {}", e)))?
            .collect::<Result<Vec<_>, _>>()
            .map_err(|e| StorageError::Database(format!("Failed to collect changes: {}", e)))?;

        Ok(entries)
    }

    /// Get changes for a specific file
    pub async fn get_file_changes(&self, path: &str) -> StorageResult<Vec<JournalEntry>> {
        debug!("Getting changes for file: {}", path);

        let conn = self.database.conn.lock().unwrap();

        let mut stmt = conn.prepare(
            "SELECT id, job_id, path, change_type, timestamp
             FROM change_journal WHERE path = ?1 ORDER BY timestamp DESC"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let entries = stmt.query_map(params![path], |row| {
            Ok(JournalEntry {
                id: row.get(0)?,
                job_id: row.get(1)?,
                path: row.get(2)?,
                change_type: row.get(3)?,
                timestamp: row.get(4)?,
            })
        }).map_err(|e| StorageError::Database(format!("Failed to query changes: {}", e)))?
            .collect::<Result<Vec<_>, _>>()
            .map_err(|e| StorageError::Database(format!("Failed to collect changes: {}", e)))?;

        Ok(entries)
    }

    /// Get changes since a specific timestamp
    pub async fn get_changes_since(&self, job_id: &str, since: i64) -> StorageResult<Vec<JournalEntry>> {
        debug!("Getting changes for job {} since {}", job_id, since);

        let conn = self.database.conn.lock().unwrap();

        let mut stmt = conn.prepare(
            "SELECT id, job_id, path, change_type, timestamp
             FROM change_journal WHERE job_id = ?1 AND timestamp > ?2 ORDER BY timestamp DESC"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let entries = stmt.query_map(params![job_id, since], |row| {
            Ok(JournalEntry {
                id: row.get(0)?,
                job_id: row.get(1)?,
                path: row.get(2)?,
                change_type: row.get(3)?,
                timestamp: row.get(4)?,
            })
        }).map_err(|e| StorageError::Database(format!("Failed to query changes: {}", e)))?
            .collect::<Result<Vec<_>, _>>()
            .map_err(|e| StorageError::Database(format!("Failed to collect changes: {}", e)))?;

        Ok(entries)
    }

    /// Clear changes for a job
    pub async fn clear_job(&self, job_id: &str) -> StorageResult<()> {
        debug!("Clearing changes for job: {}", job_id);

        let conn = self.database.conn.lock().unwrap();

        conn.execute("DELETE FROM change_journal WHERE job_id = ?1", params![job_id])
            .map_err(|e| StorageError::Database(format!("Failed to clear changes: {}", e)))?;

        Ok(())
    }

    /// Get change statistics for a job
    pub async fn get_stats(&self, job_id: &str) -> StorageResult<ChangeStats> {
        debug!("Getting change stats for job: {}", job_id);

        let conn = self.database.conn.lock().unwrap();

        let total_changes: i64 = conn.query_row(
            "SELECT COUNT(*) FROM change_journal WHERE job_id = ?1",
            params![job_id],
            |row| row.get(0),
        ).map_err(|e| StorageError::Database(format!("Failed to get total changes: {}", e)))?;

        let last_sync: Option<i64> = conn.query_row(
            "SELECT MAX(timestamp) FROM change_journal WHERE job_id = ?1 AND change_type = 'synced'",
            params![job_id],
            |row| row.get(0),
        ).ok();

        let changes_by_type = self.get_changes_by_type(job_id).await?;

        Ok(ChangeStats {
            total_changes,
            last_sync,
            changes_by_type,
        })
    }

    /// Get changes grouped by type
    async fn get_changes_by_type(&self, job_id: &str) -> StorageResult<Vec<(String, i64)>> {
        let conn = self.database.conn.lock().unwrap();

        let mut stmt = conn.prepare(
            "SELECT change_type, COUNT(*) 
             FROM change_journal WHERE job_id = ?1 GROUP BY change_type"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let results: Vec<(String, i64)> = stmt.query_map(params![job_id], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, i64>(1)?))
        }).map_err(|e| StorageError::Database(format!("Failed to query changes by type: {}", e)))?
            .collect::<Result<Vec<_>, _>>()
            .map_err(|e| StorageError::Database(format!("Failed to collect changes by type: {}", e)))?;

        Ok(results)
    }
}

/// Change statistics
#[derive(Debug, Clone)]
pub struct ChangeStats {
    /// Total number of changes
    pub total_changes: i64,
    /// Last sync timestamp
    pub last_sync: Option<i64>,
    /// Changes grouped by type
    pub changes_by_type: Vec<(String, i64)>,
}
