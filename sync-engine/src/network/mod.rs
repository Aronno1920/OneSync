//! Network module - File transfer and network protocols
//!
//! This module handles network transfer operations including:
//! - Transfer engine: Manages file transfers over the network
//! - Rsync algorithm: Implements rsync-like delta transfer
//! - Protocols: Various network protocol implementations

pub mod transfer;
pub mod rsync_algorithm;
pub mod protocols;

pub use transfer::TransferEngine;
pub use rsync_algorithm::RsyncAlgorithm;
pub use protocols::{NetworkProtocol, ProtocolConfig};

/// Network error types
#[derive(Debug, thiserror::Error)]
pub enum NetworkError {
    #[error("Connection error: {0}")]
    Connection(String),

    #[error("Transfer error: {0}")]
    Transfer(String),

    #[error("Protocol error: {0}")]
    Protocol(String),

    #[error("Timeout error")]
    Timeout,

    #[error("Authentication error: {0}")]
    Authentication(String),
}

/// Result type for network operations
pub type NetworkResult<T> = std::result::Result<T, NetworkError>;

/// Transfer statistics
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct TransferStats {
    /// Bytes transferred
    pub bytes_transferred: u64,
    /// Files transferred
    pub files_transferred: u64,
    /// Transfer start time
    pub start_time: i64,
    /// Transfer end time
    pub end_time: Option<i64>,
    /// Average speed in bytes per second
    pub avg_speed: f64,
}

impl TransferStats {
    /// Calculate average speed
    pub fn calculate_speed(&mut self) {
        if let Some(end_time) = self.end_time {
            let duration = (end_time - self.start_time) as f64;
            if duration > 0.0 {
                self.avg_speed = self.bytes_transferred as f64 / duration;
            }
        }
    }
}
