//! OneSync Engine - High-performance file synchronization engine
//! 
//! This is the main entry point for the sync engine binary. It initializes
//! the Tokio runtime, sets up logging, and starts the gRPC server for
//! communication with the .NET MAUI UI layer.

use anyhow::Result;
use clap::Parser;
use tracing::{info, error};
use tracing_subscriber::{EnvFilter, fmt};

use sync_engine::ipc::grpc_server::start_grpc_server;
use sync_engine::core::orchestrator::SyncOrchestrator;

#[derive(Parser, Debug)]
#[command(name = "sync-engine")]
#[command(about = "OneSync high-performance file synchronization engine", long_about = None)]
struct Args {
    /// gRPC server address
    #[arg(short, long, default_value = "127.0.0.1:50051")]
    addr: String,

    /// Enable verbose logging
    #[arg(short, long)]
    verbose: bool,
}

#[tokio::main]
async fn main() -> Result<()> {
    let args = Args::parse();

    // Initialize logging
    let filter = if args.verbose {
        EnvFilter::from_default_env().add_directive(tracing::Level::DEBUG.into())
    } else {
        EnvFilter::from_default_env().add_directive(tracing::Level::INFO.into())
    };

    fmt()
        .with_env_filter(filter)
        .with_target(false)
        .with_thread_ids(true)
        .init();

    info!("Starting OneSync Engine v{}", env!("CARGO_PKG_VERSION"));

    // Initialize the sync orchestrator
    let orchestrator = SyncOrchestrator::new().await?;

    // Start the gRPC server
    info!("Starting gRPC server on {}", args.addr);
    match start_grpc_server(&args.addr, orchestrator).await {
        Ok(_) => {
            info!("gRPC server started successfully");
            Ok(())
        }
        Err(e) => {
            error!("Failed to start gRPC server: {}", e);
            Err(e)
        }
    }
}
