//! Network protocols - Various network protocol implementations
//!
//! This module provides support for different network protocols for file transfer,
//! including TCP, QUIC, and custom binary protocols.

use std::net::SocketAddr;
use std::sync::Arc;
use tokio::net::{TcpListener, TcpStream};
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tracing::{debug, info, warn};
use bytes::Bytes;
use serde::{Serialize, Deserialize};

use super::{NetworkError, NetworkResult};

/// Network protocol types
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ProtocolType {
    /// TCP protocol
    Tcp,
    /// QUIC protocol
    Quic,
    /// Custom binary protocol
    Custom,
}

/// Protocol configuration
#[derive(Debug, Clone)]
pub struct ProtocolConfig {
    /// Protocol type
    pub protocol_type: ProtocolType,
    /// Server address
    pub server_addr: SocketAddr,
    /// Enable TLS
    pub enable_tls: bool,
    /// Connection timeout in seconds
    pub timeout_secs: u64,
    /// Maximum message size
    pub max_message_size: usize,
}

impl Default for ProtocolConfig {
    fn default() -> Self {
        Self {
            protocol_type: ProtocolType::Tcp,
            server_addr: "127.0.0.1:0".parse().unwrap(),
            enable_tls: false,
            timeout_secs: 30,
            max_message_size: 64 * 1024 * 1024, // 64MB
        }
    }
}

/// Network protocol trait
#[async_trait::async_trait]
pub trait NetworkProtocol: Send + Sync {
    /// Connect to a remote server
    async fn connect(&self, addr: SocketAddr) -> NetworkResult<Box<dyn ProtocolConnection + Send + Sync>>;

    /// Start a server
    async fn serve(&self, handler: Arc<dyn ProtocolHandler + Send + Sync>) -> NetworkResult<()>;
}

/// Protocol connection trait
#[async_trait::async_trait]
pub trait ProtocolConnection: Send + Sync {
    /// Send data
    async fn send(&mut self, data: &[u8]) -> NetworkResult<()>;

    /// Receive data
    async fn recv(&mut self, buf: &mut [u8]) -> NetworkResult<usize>;

    /// Close connection
    async fn close(&mut self) -> NetworkResult<()>;
}

/// Protocol handler trait
#[async_trait::async_trait]
pub trait ProtocolHandler: Send + Sync {
    /// Handle incoming connection
    async fn handle_connection(&self, conn: Box<dyn ProtocolConnection + Send + Sync>) -> NetworkResult<()>;
}

/// TCP protocol implementation
pub struct TcpProtocol {
    config: ProtocolConfig,
}

impl TcpProtocol {
    /// Create a new TCP protocol
    pub fn new(config: ProtocolConfig) -> Self {
        Self { config }
    }
}

#[async_trait::async_trait]
impl NetworkProtocol for TcpProtocol {
    async fn connect(&self, addr: SocketAddr) -> NetworkResult<Box<dyn ProtocolConnection + Send + Sync>> {
        debug!("Connecting to TCP server: {}", addr);

        let stream = tokio::time::timeout(
            std::time::Duration::from_secs(self.config.timeout_secs),
            TcpStream::connect(addr),
        )
        .await
        .map_err(|_| NetworkError::Timeout)?
        .map_err(|e| NetworkError::Connection(format!("Failed to connect: {}", e)))?;

        Ok(Box::new(TcpConnection { stream }))
    }

    async fn serve(&self, handler: Arc<dyn ProtocolHandler + Send + Sync>) -> NetworkResult<()> {
        info!("Starting TCP server on {}", self.config.server_addr);

        let listener = TcpListener::bind(self.config.server_addr)
            .await
            .map_err(|e| NetworkError::Connection(format!("Failed to bind: {}", e)))?;

        loop {
            match listener.accept().await {
                Ok((stream, addr)) => {
                    debug!("Accepted connection from: {}", addr);
                    let conn = TcpConnection { stream };
                    let handler = handler.clone();

                    tokio::spawn(async move {
                        if let Err(e) = handler.handle_connection(Box::new(conn)).await {
                            warn!("Handler error: {}", e);
                        }
                    });
                }
                Err(e) => {
                    warn!("Failed to accept connection: {}", e);
                }
            }
        }
    }
}

/// TCP connection implementation
pub struct TcpConnection {
    stream: TcpStream,
}

#[async_trait::async_trait]
impl ProtocolConnection for TcpConnection {
    async fn send(&mut self, data: &[u8]) -> NetworkResult<()> {
        self.stream.write_all(data).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to send: {}", e)))?;
        Ok(())
    }

    async fn recv(&mut self, buf: &mut [u8]) -> NetworkResult<usize> {
        let n = self.stream.read(buf).await
            .map_err(|e| NetworkError::Transfer(format!("Failed to receive: {}", e)))?;
        Ok(n)
    }

    async fn close(&mut self) -> NetworkResult<()> {
        self.stream.shutdown().await
            .map_err(|e| NetworkError::Transfer(format!("Failed to close: {}", e)))?;
        Ok(())
    }
}

/// Protocol message types
#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum ProtocolMessage {
    /// Handshake message
    Handshake { version: String, capabilities: Vec<String> },
    /// File transfer request
    FileTransferRequest { path: String, size: u64, hash: String },
    /// File transfer response
    FileTransferResponse { accepted: bool, offset: u64 },
    /// Data chunk
    DataChunk { offset: u64, data: Vec<u8> },
    /// Transfer complete
    TransferComplete { success: bool, message: String },
    /// Error message
    Error { code: i32, message: String },
}

impl ProtocolMessage {
    /// Serialize message to bytes
    pub fn to_bytes(&self) -> NetworkResult<Vec<u8>> {
        bincode::serialize(self)
            .map_err(|e| NetworkError::Protocol(format!("Failed to serialize: {}", e)))
    }

    /// Deserialize message from bytes
    pub fn from_bytes(data: &[u8]) -> NetworkResult<Self> {
        bincode::deserialize(data)
            .map_err(|e| NetworkError::Protocol(format!("Failed to deserialize: {}", e)))
    }
}

/// Default protocol handler
pub struct DefaultProtocolHandler;

#[async_trait::async_trait]
impl ProtocolHandler for DefaultProtocolHandler {
    async fn handle_connection(&self, mut conn: Box<dyn ProtocolConnection + Send + Sync>) -> NetworkResult<()> {
        let mut buf = vec![0u8; 8192];

        loop {
            let n = conn.recv(&mut buf).await?;
            if n == 0 {
                break;
            }

            let message = ProtocolMessage::from_bytes(&buf[..n])?;
            debug!("Received message: {:?}", message);

            // Handle message
            match message {
                ProtocolMessage::Handshake { version, capabilities } => {
                    info!("Handshake from client: version={}, capabilities={:?}", version, capabilities);
                    let response = ProtocolMessage::Handshake {
                        version: "1.0.0".to_string(),
                        capabilities: vec!["delta_transfer".to_string(), "compression".to_string()],
                    };
                    conn.send(&response.to_bytes()?).await?;
                }
                _ => {
                    warn!("Unhandled message type");
                }
            }
        }

        conn.close().await?;
        Ok(())
    }
}
