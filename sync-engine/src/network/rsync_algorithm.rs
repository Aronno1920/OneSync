//! Rsync algorithm - Implements rsync-like delta transfer
//!
//! This module implements the rsync algorithm for efficient delta transfer,
//! using rolling checksums and block matching to minimize data transfer.

use std::collections::HashMap;
use std::path::Path;
use std::fs::File;
use std::io::{BufReader, Read};
use memmap2::Mmap;
use blake3::Hasher;
use tracing::{debug, info};
use xxhash_rust::xxh3::xxh3_64;

use crate::core::{BlockInfo, FileDelta};
use super::{NetworkError, NetworkResult};

/// Rsync algorithm configuration
#[derive(Debug, Clone)]
pub struct RsyncConfig {
    /// Block size for chunking
    pub block_size: usize,
    /// Use rolling checksum
    pub use_rolling_checksum: bool,
    /// Hash algorithm
    pub hash_algorithm: HashAlgorithm,
}

/// Hash algorithm options
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum HashAlgorithm {
    /// BLAKE3 (default, most secure)
    Blake3,
    /// xxHash3 (fastest)
    Xxh3,
}

impl Default for RsyncConfig {
    fn default() -> Self {
        Self {
            block_size: 256 * 1024, // 256KB
            use_rolling_checksum: true,
            hash_algorithm: HashAlgorithm::Blake3,
        }
    }
}

/// Rsync algorithm implementation
#[derive(Clone)]
pub struct RsyncAlgorithm {
    config: RsyncConfig,
}

impl RsyncAlgorithm {
    /// Create a new rsync algorithm instance
    pub fn new() -> Self {
        Self {
            config: RsyncConfig::default(),
        }
    }

    /// Create a new rsync algorithm with custom config
    pub fn with_config(config: RsyncConfig) -> Self {
        Self { config }
    }

    /// Compute signature for a file
    pub async fn compute_signature(&self, path: &Path) -> NetworkResult<Vec<u8>> {
        debug!("Computing signature for file: {}", path.display());

        let file_size = std::fs::metadata(path)
            .map_err(|e| NetworkError::Transfer(format!("Failed to get file size: {}", e)))?
            .len();

        if file_size == 0 {
            return Ok(vec![]);
        }

        let mut signature = Vec::new();
        let num_blocks = ((file_size as usize) + self.config.block_size - 1) / self.config.block_size;

        // Use memory mapping for large files
        if file_size > 10 * 1024 * 1024 {
            self.compute_signature_mmap(path, num_blocks, &mut signature).await?;
        } else {
            self.compute_signature_buffered(path, num_blocks, &mut signature).await?;
        }

        Ok(signature)
    }

    /// Compute signature using memory mapping
    async fn compute_signature_mmap(
        &self,
        path: &Path,
        num_blocks: usize,
        signature: &mut Vec<u8>,
    ) -> NetworkResult<()> {
        let file = File::open(path)
            .map_err(|e| NetworkError::Transfer(format!("Failed to open file: {}", e)))?;

        let mmap = unsafe { Mmap::map(&file) }
            .map_err(|e| NetworkError::Transfer(format!("Failed to mmap file: {}", e)))?;

        for i in 0..num_blocks {
            let start = i * self.config.block_size;
            let end = std::cmp::min(start + self.config.block_size, mmap.len());
            let block_data = &mmap[start..end];

            let hash = match self.config.hash_algorithm {
                HashAlgorithm::Blake3 => {
                    let mut hasher = Hasher::new();
                    hasher.update(block_data);
                    hasher.finalize().to_vec()
                }
                HashAlgorithm::Xxh3 => {
                    xxh3_64(block_data).to_le_bytes().to_vec()
                }
            };

            signature.extend_from_slice(&hash);
        }

        Ok(())
    }

    /// Compute signature using buffered reading
    async fn compute_signature_buffered(
        &self,
        path: &Path,
        num_blocks: usize,
        signature: &mut Vec<u8>,
    ) -> NetworkResult<()> {
        let file = File::open(path)
            .map_err(|e| NetworkError::Transfer(format!("Failed to open file: {}", e)))?;

        let mut reader = BufReader::with_capacity(self.config.block_size, file);
        let mut buffer = vec![0u8; self.config.block_size];

        for _ in 0..num_blocks {
            let bytes_read = reader.read(&mut buffer)
                .map_err(|e| NetworkError::Transfer(format!("Failed to read block: {}", e)))?;

            if bytes_read == 0 {
                break;
            }

            let block_data = &buffer[..bytes_read];
            let hash = match self.config.hash_algorithm {
                HashAlgorithm::Blake3 => {
                    let mut hasher = Hasher::new();
                    hasher.update(block_data);
                    hasher.finalize().to_vec()
                }
                HashAlgorithm::Xxh3 => {
                    xxh3_64(block_data).to_le_bytes().to_vec()
                }
            };

            signature.extend_from_slice(&hash);
        }

        Ok(())
    }

    /// Compute delta between source and target signatures
    pub async fn compute_delta(
        &self,
        source_path: &Path,
        target_signature: &[u8],
    ) -> NetworkResult<FileDelta> {
        debug!("Computing delta for file: {}", source_path.display());

        let file_size = std::fs::metadata(source_path)
            .map_err(|e| NetworkError::Transfer(format!("Failed to get file size: {}", e)))?
            .len();

        if file_size == 0 {
            return Ok(FileDelta {
                source_path: source_path.to_string_lossy().to_string(),
                target_path: source_path.to_string_lossy().to_string(),
                blocks: vec![],
                total_bytes: 0,
                transfer_percentage: 0.0,
            });
        }

        // Parse target signature into block hashes
        let mut target_hashes = Vec::new();
        let hash_size = match self.config.hash_algorithm {
            HashAlgorithm::Blake3 => 32,
            HashAlgorithm::Xxh3 => 8,
        };

        for chunk in target_signature.chunks(hash_size) {
            if chunk.len() == hash_size {
                let mut hash = [0u8; 32];
                hash[..chunk.len()].copy_from_slice(chunk);
                target_hashes.push(hash);
            }
        }

        // Find different blocks
        let mut blocks = Vec::new();
        let mut total_bytes = 0u64;
        let num_blocks = ((file_size as usize) + self.config.block_size - 1) / self.config.block_size;

        let file = File::open(source_path)
            .map_err(|e| NetworkError::Transfer(format!("Failed to open file: {}", e)))?;

        let mmap = unsafe { Mmap::map(&file) }
            .map_err(|e| NetworkError::Transfer(format!("Failed to mmap file: {}", e)))?;

        for i in 0..num_blocks {
            let start = i * self.config.block_size;
            let end = std::cmp::min(start + self.config.block_size, mmap.len());
            let block_data = &mmap[start..end];

            let hash = match self.config.hash_algorithm {
                HashAlgorithm::Blake3 => {
                    let mut hasher = Hasher::new();
                    hasher.update(block_data);
                    hasher.finalize().into()
                }
                HashAlgorithm::Xxh3 => {
                    let mut hash = [0u8; 32];
                    let xxhash = xxh3_64(block_data).to_le_bytes();
                    hash[..8].copy_from_slice(&xxhash);
                    hash
                }
            };

            // Check if block matches target
            let block_matches = i < target_hashes.len() && hash == target_hashes[i];

            if !block_matches {
                blocks.push(BlockInfo {
                    index: i as u64,
                    offset: start as u64,
                    size: end - start,
                    hash,
                });
                total_bytes += (end - start) as u64;
            }
        }

        let transfer_percentage = if file_size > 0 {
            (total_bytes as f64 / file_size as f64) * 100.0
        } else {
            0.0
        };

        info!("Delta computed: {}/{} blocks ({}%)", blocks.len(), num_blocks, transfer_percentage);

        Ok(FileDelta {
            source_path: source_path.to_string_lossy().to_string(),
            target_path: source_path.to_string_lossy().to_string(),
            blocks,
            total_bytes,
            transfer_percentage,
        })
    }

    /// Patch a file using delta information
    pub async fn patch_file(
        &self,
        target_path: &Path,
        delta: &FileDelta,
        source_data: &[u8],
    ) -> NetworkResult<()> {
        debug!("Patching file: {}", target_path.display());

        let mut target_data = source_data.to_vec();

        for block in &delta.blocks {
            let start = block.offset as usize;
            let end = start + block.size;

            if end <= target_data.len() {
                // Copy block data from source
                let block_data = &source_data[start..end];
                target_data[start..end].copy_from_slice(block_data);
            }
        }

        // Write patched data to target
        fs::write(target_path, target_data).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to write patched file: {}", e)))?;

        Ok(())
    }
}

impl Default for RsyncAlgorithm {
    fn default() -> Self {
        Self::new()
    }
}
