//! Database - SQLite database for persistent storage
//!
//! This module provides a SQLite-based database for storing sync jobs,
//! file metadata, and change journals.

use std::path::Path;
use std::sync::Arc;
use rusqlite::{Connection, params, Result as SqliteResult};
use rusqlite_migration::{Migrations, M};
use tracing::{info, error, debug};
use tokio::sync::RwLock;

use crate::models::sync_job::SyncJob;
use crate::models::file_node::FileNode;
use super::{StorageError, StorageResult};

/// Database migrations
fn migrations() -> Migrations<'static> {
    Migrations::new(vec![
        M::up(
            "CREATE TABLE IF NOT EXISTS jobs (
                id TEXT PRIMARY KEY,
                source_path TEXT NOT NULL,
                target_path TEXT NOT NULL,
                direction INTEGER NOT NULL,
                conflict_strategy INTEGER NOT NULL,
                exclude_patterns TEXT,
                include_patterns TEXT,
                enable_compression INTEGER NOT NULL,
                block_size INTEGER NOT NULL,
                status INTEGER NOT NULL,
                created_at INTEGER NOT NULL,
                start_time INTEGER,
                end_time INTEGER,
                error_message TEXT
            );"
        ),
        M::up(
            "CREATE TABLE IF NOT EXISTS file_metadata (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT NOT NULL UNIQUE,
                is_directory INTEGER NOT NULL,
                size INTEGER NOT NULL,
                modified_time INTEGER NOT NULL,
                hash TEXT NOT NULL,
                job_id TEXT NOT NULL,
                FOREIGN KEY (job_id) REFERENCES jobs(id) ON DELETE CASCADE
            );"
        ),
        M::up(
            "CREATE INDEX IF NOT EXISTS idx_file_metadata_path ON file_metadata(path);
             CREATE INDEX IF NOT EXISTS idx_file_metadata_job_id ON file_metadata(job_id);"
        ),
        M::up(
            "CREATE TABLE IF NOT EXISTS change_journal (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                job_id TEXT NOT NULL,
                path TEXT NOT NULL,
                change_type TEXT NOT NULL,
                timestamp INTEGER NOT NULL,
                FOREIGN KEY (job_id) REFERENCES jobs(id) ON DELETE CASCADE
            );"
        ),
        M::up(
            "CREATE INDEX IF NOT EXISTS idx_change_journal_job_id ON change_journal(job_id);
             CREATE INDEX IF NOT EXISTS idx_change_journal_timestamp ON change_journal(timestamp);"
        ),
    ])
}

/// SQLite database for persistent storage
#[derive(Clone)]
pub struct Database {
    pub conn: Arc<RwLock<Connection>>,
    path: String,
}

impl Database {
    /// Create a new database connection
    pub async fn new(path: &str) -> StorageResult<Self> {
        info!("Opening database: {}", path);

        let conn = Connection::open(path)
            .map_err(|e| StorageError::Database(format!("Failed to open database: {}", e)))?;

        // Run migrations
        migrations()
            .to_latest(&conn)
            .map_err(|e| StorageError::Database(format!("Failed to run migrations: {}", e)))?;

        info!("Database initialized successfully");

        Ok(Self {
            conn: Arc::new(RwLock::new(conn)),
            path: path.to_string(),
        })
    }

    /// Save a sync job to the database
    pub async fn save_job(&self, job: &SyncJob) -> StorageResult<()> {
        debug!("Saving job: {}", job.id);

        let conn = self.conn.read().await;

        let exclude_patterns = serde_json::to_string(&job.config.exclude_patterns)
            .map_err(|e| StorageError::Serialization(format!("Failed to serialize exclude patterns: {}", e)))?;

        let include_patterns = serde_json::to_string(&job.config.include_patterns)
            .map_err(|e| StorageError::Serialization(format!("Failed to serialize include patterns: {}", e)))?;

        conn.execute(
            "INSERT OR REPLACE INTO jobs (
                id, source_path, target_path, direction, conflict_strategy,
                exclude_patterns, include_patterns, enable_compression, block_size,
                status, created_at, start_time, end_time, error_message
            ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14)",
            params![
                job.id,
                job.config.source_path,
                job.config.target_path,
                job.config.direction as i32,
                job.config.conflict_strategy as i32,
                exclude_patterns,
                include_patterns,
                job.config.enable_compression as i32,
                job.config.block_size as i32,
                job.status as i32,
                job.created_at.timestamp(),
                job.start_time.map(|t| t.timestamp()),
                job.end_time.map(|t| t.timestamp()),
                job.error_message,
            ],
        ).map_err(|e| StorageError::Database(format!("Failed to save job: {}", e)))?;

        Ok(())
    }

    /// Load a sync job from the database
    pub async fn load_job(&self, job_id: &str) -> StorageResult<SyncJob> {
        debug!("Loading job: {}", job_id);

        let conn = self.conn.read().await;

        let mut stmt = conn.prepare(
            "SELECT id, source_path, target_path, direction, conflict_strategy,
                    exclude_patterns, include_patterns, enable_compression, block_size,
                    status, created_at, start_time, end_time, error_message
             FROM jobs WHERE id = ?1"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let job = stmt.query_row(params![job_id], |row| {
            let exclude_patterns: String = row.get(5)?;
            let include_patterns: String = row.get(6)?;
            
            let direction_val: i32 = row.get(3)?;
            let direction = match direction_val {
                0 => crate::models::sync_job::SyncDirection::Bidirectional,
                1 => crate::models::sync_job::SyncDirection::SourceToTarget,
                2 => crate::models::sync_job::SyncDirection::TargetToSource,
                _ => crate::models::sync_job::SyncDirection::Bidirectional,
            };
            
            let conflict_strategy_val: i32 = row.get(4)?;
            let conflict_strategy = match conflict_strategy_val {
                0 => crate::models::sync_job::ConflictStrategy::LastWriteWins,
                1 => crate::models::sync_job::ConflictStrategy::Manual,
                2 => crate::models::sync_job::ConflictStrategy::Skip,
                _ => crate::models::sync_job::ConflictStrategy::LastWriteWins,
            };
            
            let status_val: i32 = row.get(9)?;
            let status = match status_val {
                0 => crate::models::sync_job::SyncStatus::Idle,
                1 => crate::models::sync_job::SyncStatus::Scanning,
                2 => crate::models::sync_job::SyncStatus::Comparing,
                3 => crate::models::sync_job::SyncStatus::Transferring,
                4 => crate::models::sync_job::SyncStatus::ResolvingConflicts,
                5 => crate::models::sync_job::SyncStatus::Completed,
                6 => crate::models::sync_job::SyncStatus::Failed,
                7 => crate::models::sync_job::SyncStatus::Paused,
                _ => crate::models::sync_job::SyncStatus::Idle,
            };

            Ok(SyncJob {
                id: row.get(0)?,
                config: crate::models::sync_job::SyncJobConfig {
                    source_path: row.get(1)?,
                    target_path: row.get(2)?,
                    direction,
                    conflict_strategy,
                    exclude_patterns: serde_json::from_str(&exclude_patterns).unwrap_or_default(),
                    include_patterns: serde_json::from_str(&include_patterns).unwrap_or_default(),
                    enable_compression: row.get(7)?,
                    block_size: row.get(8)?,
                },
                status,
                created_at: chrono::DateTime::from_timestamp(row.get(10)?, 0).unwrap(),
                start_time: row.get::<_, Option<i64>>(11)?.map(|t| chrono::DateTime::from_timestamp(t, 0).unwrap()),
                end_time: row.get::<_, Option<i64>>(12)?.map(|t| chrono::DateTime::from_timestamp(t, 0).unwrap()),
                error_message: row.get(13)?,
                progress: Default::default(),
                conflicts: Vec::new(),
            })
        }).map_err(|e| StorageError::Database(format!("Failed to load job: {}", e)))?;

        Ok(job)
    }

    /// List all jobs
    pub async fn list_jobs(&self) -> StorageResult<Vec<SyncJob>> {
        debug!("Listing all jobs");

        let conn = self.conn.read().await;

        let mut stmt = conn.prepare(
            "SELECT id, source_path, target_path, direction, conflict_strategy,
                    exclude_patterns, include_patterns, enable_compression, block_size,
                    status, created_at, start_time, end_time, error_message
             FROM jobs"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let jobs = stmt.query_map([], |row| {
            let exclude_patterns: String = row.get(5)?;
            let include_patterns: String = row.get(6)?;
            
            let direction_val: i32 = row.get(3)?;
            let direction = match direction_val {
                0 => crate::models::sync_job::SyncDirection::Bidirectional,
                1 => crate::models::sync_job::SyncDirection::SourceToTarget,
                2 => crate::models::sync_job::SyncDirection::TargetToSource,
                _ => crate::models::sync_job::SyncDirection::Bidirectional,
            };
            
            let conflict_strategy_val: i32 = row.get(4)?;
            let conflict_strategy = match conflict_strategy_val {
                0 => crate::models::sync_job::ConflictStrategy::LastWriteWins,
                1 => crate::models::sync_job::ConflictStrategy::Manual,
                2 => crate::models::sync_job::ConflictStrategy::Skip,
                _ => crate::models::sync_job::ConflictStrategy::LastWriteWins,
            };
            
            let status_val: i32 = row.get(9)?;
            let status = match status_val {
                0 => crate::models::sync_job::SyncStatus::Idle,
                1 => crate::models::sync_job::SyncStatus::Scanning,
                2 => crate::models::sync_job::SyncStatus::Comparing,
                3 => crate::models::sync_job::SyncStatus::Transferring,
                4 => crate::models::sync_job::SyncStatus::ResolvingConflicts,
                5 => crate::models::sync_job::SyncStatus::Completed,
                6 => crate::models::sync_job::SyncStatus::Failed,
                7 => crate::models::sync_job::SyncStatus::Paused,
                _ => crate::models::sync_job::SyncStatus::Idle,
            };

            Ok(SyncJob {
                id: row.get(0)?,
                config: crate::models::sync_job::SyncJobConfig {
                    source_path: row.get(1)?,
                    target_path: row.get(2)?,
                    direction,
                    conflict_strategy,
                    exclude_patterns: serde_json::from_str(&exclude_patterns).unwrap_or_default(),
                    include_patterns: serde_json::from_str(&include_patterns).unwrap_or_default(),
                    enable_compression: row.get(7)?,
                    block_size: row.get(8)?,
                },
                status,
                created_at: chrono::DateTime::from_timestamp(row.get(10)?, 0).unwrap(),
                start_time: row.get::<_, Option<i64>>(11)?.map(|t| chrono::DateTime::from_timestamp(t, 0).unwrap()),
                end_time: row.get::<_, Option<i64>>(12)?.map(|t| chrono::DateTime::from_timestamp(t, 0).unwrap()),
                error_message: row.get(13)?,
                progress: Default::default(),
                conflicts: Vec::new(),
            })
        }).map_err(|e| StorageError::Database(format!("Failed to list jobs: {}", e)))?
            .collect::<SqliteResult<Vec<_>>>()
            .map_err(|e| StorageError::Database(format!("Failed to collect jobs: {}", e)))?;

        Ok(jobs)
    }

    /// Delete a job
    pub async fn delete_job(&self, job_id: &str) -> StorageResult<()> {
        debug!("Deleting job: {}", job_id);

        let conn = self.conn.read().await;

        conn.execute("DELETE FROM jobs WHERE id = ?1", params![job_id])
            .map_err(|e| StorageError::Database(format!("Failed to delete job: {}", e)))?;

        Ok(())
    }

    /// Save file metadata
    pub async fn save_file_metadata(&self, file: &FileNode, job_id: &str) -> StorageResult<()> {
        debug!("Saving file metadata: {}", file.path);

        let conn = self.conn.read().await;

        conn.execute(
            "INSERT OR REPLACE INTO file_metadata (path, is_directory, size, modified_time, hash, job_id)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![
                file.path,
                file.is_directory as i32,
                file.size as i64,
                file.modified_time,
                file.hash,
                job_id,
            ],
        ).map_err(|e| StorageError::Database(format!("Failed to save file metadata: {}", e)))?;

        Ok(())
    }

    /// Load file metadata
    pub async fn load_file_metadata(&self, path: &str) -> StorageResult<FileNode> {
        debug!("Loading file metadata: {}", path);

        let conn = self.conn.read().await;

        let mut stmt = conn.prepare(
            "SELECT path, is_directory, size, modified_time, hash
             FROM file_metadata WHERE path = ?1"
        ).map_err(|e| StorageError::Database(format!("Failed to prepare statement: {}", e)))?;

        let file = stmt.query_row(params![path], |row| {
            Ok(FileNode {
                path: row.get(0)?,
                is_directory: row.get(1)?,
                size: row.get(2)?,
                modified_time: row.get(3)?,
                hash: row.get(4)?,
                children: Vec::new(),
            })
        }).map_err(|e| StorageError::Database(format!("Failed to load file metadata: {}", e)))?;

        Ok(file)
    }

    /// Get database path
    pub fn path(&self) -> &str {
        &self.path
    }
}
