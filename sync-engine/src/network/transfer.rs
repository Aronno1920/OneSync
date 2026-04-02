//! Transfer engine - Manages file transfers
//!
//! This module provides efficient file transfer capabilities with support for
//! compression, parallel transfers, and delta transfers.

use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::sync::Semaphore;
use tokio::fs;
use tracing::{debug, info};
use chrono::Utc;

use crate::core::FileDelta;
use super::{NetworkError, NetworkResult, TransferStats};

/// Transfer engine configuration
#[derive(Debug, Clone)]
pub struct TransferConfig {
    /// Maximum concurrent transfers
    pub max_concurrent_transfers: usize,
    /// Enable compression
    pub enable_compression: bool,
    /// Compression level (0-9)
    pub compression_level: u8,
    /// Buffer size for transfers
    pub buffer_size: usize,
}

impl Default for TransferConfig {
    fn default() -> Self {
        Self {
            max_concurrent_transfers: 8,
            enable_compression: true,
            compression_level: 3,
            buffer_size: 64 * 1024, // 64KB
        }
    }
}

/// Transfer engine - manages file transfers
#[derive(Clone)]
pub struct TransferEngine {
    config: TransferConfig,
    semaphore: Arc<Semaphore>,
}

impl TransferEngine {
    /// Create a new transfer engine
    pub fn new() -> Self {
        let config = TransferConfig::default();
        let semaphore = Arc::new(Semaphore::new(config.max_concurrent_transfers));

        Self { config, semaphore }
    }

    /// Create a new transfer engine with custom config
    pub fn with_config(config: TransferConfig) -> Self {
        let semaphore = Arc::new(Semaphore::new(config.max_concurrent_transfers));

        Self { config, semaphore }
    }

    /// Transfer a file based on delta information
    pub async fn transfer_file(&self, delta: &FileDelta) -> NetworkResult<TransferStats> {
        info!("Transferring file: {} -> {}", delta.source_path, delta.target_path);

        let source_path = PathBuf::from(&delta.source_path);
        let target_path = PathBuf::from(&delta.target_path);

        // Create target directory if it doesn't exist
        if let Some(parent) = target_path.parent() {
            fs::create_dir_all(parent).await
                .map_err(|e| NetworkError::Transfer(format!("Failed to create directory: {}", e)))?;
        }

        if delta.blocks.is_empty() {
            // Full file transfer
            let bytes_transferred = self.transfer_full_file(&source_path, &target_path).await?;
            
            let end_time = Utc::now().timestamp();
            let mut stats = TransferStats {
                bytes_transferred,
                files_transferred: 1,
                start_time: Utc::now().timestamp(),
                end_time: Some(end_time),
                avg_speed: 0.0,
            };
            stats.calculate_speed();
            
            info!("File transfer completed: {} bytes in {:.2}s", bytes_transferred, end_time - stats.start_time);
            return Ok(stats);
        } else {
            // Delta transfer
            let bytes_transferred = self.transfer_delta_file(&source_path, &target_path, delta).await?;
            
            let end_time = Utc::now().timestamp();
            let mut stats = TransferStats {
                bytes_transferred,
                files_transferred: 1,
                start_time: Utc::now().timestamp(),
                end_time: Some(end_time),
                avg_speed: 0.0,
            };
            stats.calculate_speed();
            
            info!("Delta transfer completed: {} bytes in {:.2}s", bytes_transferred, end_time - stats.start_time);
            return Ok(stats);
        }
    }

    /// Transfer a full file
    async fn transfer_full_file(&self, source: &Path, target: &Path) -> NetworkResult<u64> {
        debug!("Full file transfer: {} -> {}", source.display(), target.display());

        let _permit = self.semaphore.acquire().await
            .map_err(|e| NetworkError::Transfer(format!("Failed to acquire semaphore: {}", e)))?;

        let source_data = fs::read(source).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to read from source: {}", e)))?;

        let data_to_write = if self.config.enable_compression {
            self.compress_data(&source_data)?
        } else {
            source_data.to_vec()
        };

        fs::write(target, data_to_write).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to write to target: {}", e)))?;

        Ok(data_to_write.len() as u64)
    }

    /// Transfer file deltas
    async fn transfer_delta_file(&self, source: &Path, target: &Path, delta: &FileDelta) -> NetworkResult<u64> {
        debug!("Delta file transfer: {} -> {} ({} blocks)", source.display(), target.display(), delta.blocks.len());

        let _permit = self.semaphore.acquire().await
            .map_err(|e| NetworkError::Transfer(format!("Failed to acquire semaphore: {}", e)))?;

        let source_data = fs::read(source).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to read from source: {}", e)))?;

        let mut total_bytes = 0u64;

        for block in &delta.blocks {
            let start = block.offset as usize;
            let end = start + block.size;

            if end <= source_data.len() {
                let block_data = source_data[start..end].to_vec();
                let data_to_write = if self.config.enable_compression {
                    self.compress_data(&block_data)?
                } else {
                    block_data
                };

                fs::write(target, data_to_write).await
                    .map_err(|e| NetworkError::Transfer(format!("Failed to write block: {}", e)))?;

                total_bytes += block.size as u64;
            }
        }

        Ok(total_bytes)
    }

    /// Compress data using zstd
    fn compress_data(&self, data: &[u8]) -> NetworkResult<Vec<u8>> {
        use zstd::encode_all;

        let compressed = encode_all(data, self.config.compression_level as i32)
            .map_err(|e| NetworkError::Transfer(format!("Compression failed: {}", e)))?;

        Ok(compressed)
    }

    /// Decompress data using zstd
    fn decompress_data(&self, data: &[u8]) -> NetworkResult<Vec<u8>> {
        use zstd::decode_all;

        let decompressed = decode_all(data)
            .map_err(|e| NetworkError::Transfer(format!("Decompression failed: {}", e)))?;

        Ok(decompressed)
    }
}

impl Default for TransferEngine {
    fn default() -> Self {
        Self::new()
    }
}
