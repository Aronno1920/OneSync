//! Transfer engine - Manages file transfers
//!
//! This module provides efficient file transfer capabilities with support for
//! compression, parallel transfers, and delta transfers.

use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::fs;
use tokio::sync::Semaphore;
use tracing::{debug, info, warn};
use chrono::Utc;

use crate::core::{FileDelta, BlockInfo};
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

        let start_time = Utc::now().timestamp();
        let mut bytes_transferred = 0u64;

        let source_path = PathBuf::from(&delta.source_path);
        let target_path = PathBuf::from(&delta.target_path);

        // Create target directory if it doesn't exist
        if let Some(parent) = target_path.parent() {
            fs::create_dir_all(parent).await
                .map_err(|e| NetworkError::Transfer(format!("Failed to create directory: {}", e)))?;
        }

        if delta.blocks.is_empty() {
            // Full file transfer
            bytes_transferred = self.transfer_full_file(&source_path, &target_path).await?;
        } else {
            // Delta transfer
            bytes_transferred = self.transfer_delta_file(&source_path, &target_path, delta).await?;
        }

        let end_time = Utc::now().timestamp();
        let mut stats = TransferStats {
            bytes_transferred,
            files_transferred: 1,
            start_time,
            end_time: Some(end_time),
            avg_speed: 0.0,
        };
        stats.calculate_speed();

        info!("File transfer completed: {} bytes in {:.2}s", bytes_transferred, end_time - start_time);
        Ok(stats)
    }

    /// Transfer a full file
    async fn transfer_full_file(&self, source: &Path, target: &Path) -> NetworkResult<u64> {
        debug!("Full file transfer: {} -> {}", source.display(), target.display());

        let _permit = self.semaphore.acquire().await
            .map_err(|e| NetworkError::Transfer(format!("Failed to acquire semaphore: {}", e)))?;

        let mut source_file = fs::File::open(source).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to open source file: {}", e)))?;

        let mut target_file = fs::File::create(target).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to create target file: {}", e)))?;

        let mut buffer = vec![0u8; self.config.buffer_size];
        let mut total_bytes = 0u64;

        loop {
            let bytes_read = source_file.read(&mut buffer).await
                .map_err(|e| NetworkError::Transfer(format!("Failed to read from source: {}", e)))?;

            if bytes_read == 0 {
                break;
            }

            let data = &buffer[..bytes_read];
            let data_to_write = if self.config.enable_compression {
                self.compress_data(data)?
            } else {
                data.to_vec()
            };

            target_file.write_all(&data_to_write).await
                .map_err(|e| NetworkError::Transfer(format!("Failed to write to target: {}", e)))?;

            total_bytes += bytes_read as u64;
        }

        Ok(total_bytes)
    }

    /// Transfer file deltas
    async fn transfer_delta_file(&self, source: &Path, target: &Path, delta: &FileDelta) -> NetworkResult<u64> {
        debug!("Delta file transfer: {} -> {} ({} blocks)", source.display(), target.display(), delta.blocks.len());

        let _permit = self.semaphore.acquire().await
            .map_err(|e| NetworkError::Transfer(format!("Failed to acquire semaphore: {}", e)))?;

        let mut source_file = fs::File::open(source).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to open source file: {}", e)))?;

        let mut target_file = fs::File::create(target).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to create target file: {}", e)))?;

        let mut total_bytes = 0u64;

        for block in &delta.blocks {
            source_file.seek(std::io::SeekFrom::Start(block.offset)).await
                .map_err(|e| NetworkError::Transfer(format!("Failed to seek in source file: {}", e)))?;

            let mut buffer = vec![0u8; block.size];
            source_file.read_exact(&mut buffer).await
                .map_err(|e| NetworkError::Transfer(format!("Failed to read block: {}", e)))?;

            let data_to_write = if self.config.enable_compression {
                self.compress_data(&buffer)?
            } else {
                buffer
            };

            target_file.write_all(&data_to_write).await
                .map_err(|e| NetworkError::Transfer(format!("Failed to write block: {}", e)))?;

            total_bytes += block.size as u64;
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
