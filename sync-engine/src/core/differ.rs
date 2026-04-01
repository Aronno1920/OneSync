//! Block differ - Computes delta differences between files
//!
//! The block differ implements rsync-like rolling checksum algorithms to
//! efficiently compute which blocks need to be transferred between files.

use std::path::Path;
use std::fs::File;
use std::io::{BufReader, Read};
use memmap2::Mmap;
use blake3::Hasher;
use tracing::{debug, info};
use xxhash_rust::xxh3::xxh3_64;

use crate::models::file_node::FileNode;
use super::{SyncError, SyncResult, BlockInfo, FileDelta};

/// Block differ configuration
#[derive(Debug, Clone)]
pub struct DifferConfig {
    /// Default block size
    pub default_block_size: usize,
    /// Use rolling checksum
    pub use_rolling_checksum: bool,
    /// Hash algorithm to use
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

impl Default for DifferConfig {
    fn default() -> Self {
        Self {
            default_block_size: 256 * 1024, // 256KB
            use_rolling_checksum: true,
            hash_algorithm: HashAlgorithm::Blake3,
        }
    }
}

/// Block differ - computes file deltas
#[derive(Clone)]
pub struct BlockDiffer {
    config: DifferConfig,
}

impl BlockDiffer {
    /// Create a new block differ
    pub fn new() -> Self {
        Self {
            config: DifferConfig::default(),
        }
    }

    /// Create a new block differ with custom config
    pub fn with_config(config: DifferConfig) -> Self {
        Self { config }
    }

    /// Compute delta between source and target files
    pub async fn compute_delta(
        &self,
        source_file: &FileNode,
        target_file: &FileNode,
        block_size: usize,
    ) -> SyncResult<FileDelta> {
        debug!("Computing delta for {} -> {}", source_file.path, target_file.path);

        let source_path = Path::new(&source_file.path);
        let target_path = Path::new(&target_file.path);

        if !source_path.exists() {
            return Err(SyncError::FileNotFound(format!("Source file not found: {}", source_path.display())));
        }

        if !target_path.exists() {
            // Target doesn't exist, need full transfer
            return Ok(FileDelta {
                source_path: source_file.path.clone(),
                target_path: target_file.path.clone(),
                blocks: vec![],
                total_bytes: source_file.size as u64,
                transfer_percentage: 100.0,
            });
        }

        // Compute block hashes for target file
        let target_blocks = self.compute_block_hashes(target_path, block_size).await?;

        // Find which blocks are different in source file
        let (blocks, total_bytes) = self.find_different_blocks(source_path, &target_blocks, block_size).await?;

        let transfer_percentage = if source_file.size > 0 {
            (total_bytes as f64 / source_file.size as f64) * 100.0
        } else {
            0.0
        };

        info!("Delta computed: {}/{} blocks ({}%)", blocks.len(), target_blocks.len(), transfer_percentage);

        Ok(FileDelta {
            source_path: source_file.path.clone(),
            target_path: target_file.path.clone(),
            blocks,
            total_bytes,
            transfer_percentage,
        })
    }

    /// Compute block hashes for a file
    async fn compute_block_hashes(&self, path: &Path, block_size: usize) -> SyncResult<Vec<[u8; 32]>> {
        let file_size = std::fs::metadata(path)
            .map_err(|e| SyncError::Differ(format!("Failed to get file size: {}", e)))?
            .len();

        if file_size == 0 {
            return Ok(vec![]);
        }

        let mut blocks = Vec::new();
        let num_blocks = ((file_size as usize) + block_size - 1) / block_size;

        // Use memory mapping for large files
        if file_size > 10 * 1024 * 1024 {
            self.compute_block_hashes_mmap(path, block_size, num_blocks, &mut blocks).await?;
        } else {
            self.compute_block_hashes_buffered(path, block_size, num_blocks, &mut blocks).await?;
        }

        Ok(blocks)
    }

    /// Compute block hashes using memory mapping
    async fn compute_block_hashes_mmap(
        &self,
        path: &Path,
        block_size: usize,
        num_blocks: usize,
        blocks: &mut Vec<[u8; 32]>,
    ) -> SyncResult<()> {
        let file = File::open(path)
            .map_err(|e| SyncError::Differ(format!("Failed to open file: {}", e)))?;

        let mmap = unsafe { Mmap::map(&file) }
            .map_err(|e| SyncError::Differ(format!("Failed to mmap file: {}", e)))?;

        for i in 0..num_blocks {
            let start = i * block_size;
            let end = std::cmp::min(start + block_size, mmap.len());
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

            blocks.push(hash);
        }

        Ok(())
    }

    /// Compute block hashes using buffered reading
    async fn compute_block_hashes_buffered(
        &self,
        path: &Path,
        block_size: usize,
        num_blocks: usize,
        blocks: &mut Vec<[u8; 32]>,
    ) -> SyncResult<()> {
        let file = File::open(path)
            .map_err(|e| SyncError::Differ(format!("Failed to open file: {}", e)))?;

        let mut reader = BufReader::with_capacity(block_size, file);
        let mut buffer = vec![0u8; block_size];

        for _ in 0..num_blocks {
            let bytes_read = reader.read(&mut buffer)
                .map_err(|e| SyncError::Differ(format!("Failed to read block: {}", e)))?;

            if bytes_read == 0 {
                break;
            }

            let block_data = &buffer[..bytes_read];
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

            blocks.push(hash);
        }

        Ok(())
    }

    /// Find different blocks in source file
    async fn find_different_blocks(
        &self,
        path: &Path,
        target_blocks: &[[u8; 32]],
        block_size: usize,
    ) -> SyncResult<(Vec<BlockInfo>, u64)> {
        let file_size = std::fs::metadata(path)
            .map_err(|e| SyncError::Differ(format!("Failed to get file size: {}", e)))?
            .len();

        if file_size == 0 || target_blocks.is_empty() {
            return Ok((vec![], 0));
        }

        let mut blocks = Vec::new();
        let mut total_bytes = 0u64;
        let num_blocks = ((file_size as usize) + block_size - 1) / block_size;

        // Use memory mapping for large files
        if file_size > 10 * 1024 * 1024 {
            self.find_different_blocks_mmap(path, target_blocks, block_size, num_blocks, &mut blocks, &mut total_bytes).await?;
        } else {
            self.find_different_blocks_buffered(path, target_blocks, block_size, num_blocks, &mut blocks, &mut total_bytes).await?;
        }

        Ok((blocks, total_bytes))
    }

    /// Find different blocks using memory mapping
    async fn find_different_blocks_mmap(
        &self,
        path: &Path,
        target_blocks: &[[u8; 32]],
        block_size: usize,
        num_blocks: usize,
        blocks: &mut Vec<BlockInfo>,
        total_bytes: &mut u64,
    ) -> SyncResult<()> {
        let file = File::open(path)
            .map_err(|e| SyncError::Differ(format!("Failed to open file: {}", e)))?;

        let mmap = unsafe { Mmap::map(&file) }
            .map_err(|e| SyncError::Differ(format!("Failed to mmap file: {}", e)))?;

        for i in 0..num_blocks {
            let start = i * block_size;
            let end = std::cmp::min(start + block_size, mmap.len());
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

            // Compare with target block
            if i >= target_blocks.len() || hash != target_blocks[i] {
                blocks.push(BlockInfo {
                    index: i as u64,
                    offset: start as u64,
                    size: end - start,
                    hash,
                });
                *total_bytes += (end - start) as u64;
            }
        }

        Ok(())
    }

    /// Find different blocks using buffered reading
    async fn find_different_blocks_buffered(
        &self,
        path: &Path,
        target_blocks: &[[u8; 32]],
        block_size: usize,
        num_blocks: usize,
        blocks: &mut Vec<BlockInfo>,
        total_bytes: &mut u64,
    ) -> SyncResult<()> {
        let file = File::open(path)
            .map_err(|e| SyncError::Differ(format!("Failed to open file: {}", e)))?;

        let mut reader = BufReader::with_capacity(block_size, file);
        let mut buffer = vec![0u8; block_size];

        for i in 0..num_blocks {
            let bytes_read = reader.read(&mut buffer)
                .map_err(|e| SyncError::Differ(format!("Failed to read block: {}", e)))?;

            if bytes_read == 0 {
                break;
            }

            let block_data = &buffer[..bytes_read];
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

            // Compare with target block
            if i >= target_blocks.len() || hash != target_blocks[i] {
                blocks.push(BlockInfo {
                    index: i as u64,
                    offset: (i * block_size) as u64,
                    size: bytes_read,
                    hash,
                });
                *total_bytes += bytes_read as u64;
            }
        }

        Ok(())
    }
}
