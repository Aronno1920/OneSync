//! gRPC server implementation for IPC communication
//!
//! This module implements the gRPC server that handles requests from the
//! .NET MAUI UI layer and forwards them to the sync orchestrator.
//!
//! NOTE: For initial testing without Protocol Buffers, this is a simplified version.
//! To enable full gRPC functionality:
//! 1. Install protoc (Protocol Buffers compiler)
//! 2. Uncomment the include!() line below
//! 3. Uncomment tonic imports and implementations
//! 4. Uncomment build.rs to compile protocol.proto

// Include generated protocol code (disabled for initial testing)
// include!(concat!(env!("OUT_DIR"), "/sync_engine.rs"));

use std::sync::Arc;
use tokio::sync::RwLock;
// use tonic::{Request, Response, Status, transport::Server};
use tracing::{info, error, debug};
use uuid::Uuid;

use crate::core::orchestrator::SyncOrchestrator;
use crate::models::sync_job::{SyncJob, SyncJobConfig, SyncDirection, SyncStatus};
// use sync_engine_server::SyncEngine;

/// gRPC server implementation (simplified for testing)
#[derive(Clone)]
pub struct SyncEngineServer {
    orchestrator: Arc<RwLock<SyncOrchestrator>>,
}

impl SyncEngineServer {
    /// Create a new gRPC server instance
    pub fn new(orchestrator: Arc<RwLock<SyncOrchestrator>>) -> Self {
        Self { orchestrator }
    }
}

// #[tonic::async_trait]
// impl SyncEngine for SyncEngineServer {
//     /// Create a new sync job
//     async fn create_job(
//         &self,
//         request: Request<CreateJobRequest>,
//     ) -> Result<Response<CreateJobResponse>, Status> {
//         let req = request.into_inner();
//         debug!("Received create_job request: {:?}", req);
// 
//         let job_id = Uuid::new_v4().to_string();
//         let config = SyncJobConfig {
//             source_path: req.source_path.clone(),
//             target_path: req.target_path.clone(),
//             direction: match req.direction() {
//                 super::protocol::SyncDirection::Bidirectional => SyncDirection::Bidirectional,
//                 super::protocol::SyncDirection::SourceToTarget => SyncDirection::SourceToTarget,
//                 super::protocol::SyncDirection::TargetToSource => SyncDirection::TargetToSource,
//             },
//             conflict_strategy: match req.conflict_strategy() {
//                 super::protocol::ConflictStrategy::LastWriteWins => crate::models::sync_job::ConflictStrategy::LastWriteWins,
//                 super::protocol::ConflictStrategy::Manual => crate::models::sync_job::ConflictStrategy::Manual,
//                 super::protocol::ConflictStrategy::Skip => crate::models::sync_job::ConflictStrategy::Skip,
//             },
//             exclude_patterns: req.exclude_patterns,
//             include_patterns: req.include_patterns,
//             enable_compression: req.enable_compression,
//             block_size: req.block_size as usize,
//         };
// 
//         let job = SyncJob::new(job_id.clone(), config);
//         
//         // Store job in orchestrator
//         let mut orchestrator = self.orchestrator.write().await;
//         orchestrator.add_job(job).await.map_err(|e| {
//             error!("Failed to add job: {}", e);
//             Status::internal(format!("Failed to create job: {}", e))
//         })?;
// 
//         info!("Created sync job with ID: {}", job_id);
//         Ok(Response::new(CreateJobResponse {
//             job_id,
//             success: true,
//             message: "Job created successfully".to_string(),
//         }))
//     }
// 
//     /// Start a sync job
//     async fn start_job(
//         &self,
//         request: Request<StartJobRequest>,
//     ) -> Result<Response<StartJobResponse>, Status> {
//         let req = request.into_inner();
//         debug!("Received start_job request for job: {}", req.job_id);
// 
//         let mut orchestrator = self.orchestrator.write().await;
//         orchestrator.start_job(&req.job_id).await.map_err(|e| {
//             error!("Failed to start job {}: {}", req.job_id, e);
//             Status::internal(format!("Failed to start job: {}", e))
//         })?;
// 
//         info!("Started sync job: {}", req.job_id);
//         Ok(Response::new(StartJobResponse {
//             success: true,
//             message: "Job started successfully".to_string(),
//         }))
//     }
// 
//     /// Stop a sync job
//     async fn stop_job(
//         &self,
//         request: Request<StopJobRequest>,
//     ) -> Result<Response<StopJobResponse>, Status> {
//         let req = request.into_inner();
//         debug!("Received stop_job request for job: {}", req.job_id);
// 
//         let mut orchestrator = self.orchestrator.write().await;
//         orchestrator.stop_job(&req.job_id).await.map_err(|e| {
//             error!("Failed to stop job {}: {}", req.job_id, e);
//             Status::internal(format!("Failed to stop job: {}", e))
//         })?;
// 
//         info!("Stopped sync job: {}", req.job_id);
//         Ok(Response::new(StopJobResponse {
//             success: true,
//             message: "Job stopped successfully".to_string(),
//         }))
//     }
// 
//     /// Get job status
//     async fn job_status(
//         &self,
//         request: Request<JobStatusRequest>,
//     ) -> Result<Response<JobStatusResponse>, Status> {
//         let req = request.into_inner();
//         debug!("Received job_status request for job: {}", req.job_id);
// 
//         let orchestrator = self.orchestrator.read().await;
//         let job = orchestrator.get_job(&req.job_id).await.ok_or_else(|| {
//             Status::not_found(format!("Job not found: {}", req.job_id))
//         })?;
// 
//         let status = match job.status {
//             SyncStatus::Idle => super::protocol::JobStatus::Idle,
//             SyncStatus::Scanning => super::protocol::JobStatus::Scanning,
//             SyncStatus::Comparing => super::protocol::JobStatus::Comparing,
//             SyncStatus::Transferring => super::protocol::JobStatus::Transferring,
//             SyncStatus::ResolvingConflicts => super::protocol::JobStatus::ResolvingConflicts,
//             SyncStatus::Completed => super::protocol::JobStatus::Completed,
//             SyncStatus::Failed => super::protocol::JobStatus::Failed,
//             SyncStatus::Paused => super::protocol::JobStatus::Paused,
//         };
// 
//         let progress = SyncProgress {
//             files_scanned: job.progress.files_scanned as u64,
//             files_transferred: job.progress.files_transferred as u64,
//             bytes_transferred: job.progress.bytes_transferred,
//             total_files: job.progress.total_files as u64,
//             total_bytes: job.progress.total_bytes,
//             percentage: job.progress.percentage(),
//         };
// 
//         Ok(Response::new(JobStatusResponse {
//             job_id: job.id.clone(),
//             status: status as i32,
//             progress: Some(progress),
//             error_message: job.error_message.clone(),
//             start_time: job.start_time.map(|t| t.timestamp()),
//             end_time: job.end_time.map(|t| t.timestamp()),
//         }))
//     }
// 
//     /// List all sync jobs
//     async fn list_jobs(
//         &self,
//         _request: Request<ListJobsRequest>,
//     ) -> Result<Response<ListJobsResponse>, Status> {
//         debug!("Received list_jobs request");
// 
//         let orchestrator = self.orchestrator.read().await;
//         let jobs = orchestrator.list_jobs().await;
// 
//         let job_summaries = jobs.iter().map(|job| super::protocol::JobSummary {
//             job_id: job.id.clone(),
//             source_path: job.config.source_path.clone(),
//             target_path: job.config.target_path.clone(),
//             status: match job.status {
//                 SyncStatus::Idle => super::protocol::JobStatus::Idle,
//                 SyncStatus::Scanning => super::protocol::JobStatus::Scanning,
//                 SyncStatus::Comparing => super::protocol::JobStatus::Comparing,
//                 SyncStatus::Transferring => super::protocol::JobStatus::Transferring,
//                 SyncStatus::ResolvingConflicts => super::protocol::JobStatus::ResolvingConflicts,
//                 SyncStatus::Completed => super::protocol::JobStatus::Completed,
//                 SyncStatus::Failed => super::protocol::JobStatus::Failed,
//                 SyncStatus::Paused => super::protocol::JobStatus::Paused,
//             } as i32,
//             created_at: job.created_at.timestamp(),
//         }).collect();
// 
//         Ok(Response::new(ListJobsResponse {
//             jobs: job_summaries,
//         }))
//     }
// 
//     /// Resolve a conflict
//     async fn resolve_conflict(
//         &self,
//         request: Request<ResolveConflictRequest>,
//     ) -> Result<Response<ResolveConflictResponse>, Status> {
//         let req = request.into_inner();
//         debug!("Received resolve_conflict request for job: {}", req.job_id);
// 
//         let mut orchestrator = self.orchestrator.write().await;
//         orchestrator.resolve_conflict(&req.job_id, &req.conflict_id, req.resolution).await.map_err(|e| {
//             error!("Failed to resolve conflict: {}", e);
//             Status::internal(format!("Failed to resolve conflict: {}", e))
//         })?;
// 
//         info!("Resolved conflict {} for job {}", req.conflict_id, req.job_id);
//         Ok(Response::new(ResolveConflictResponse {
//             success: true,
//             message: "Conflict resolved successfully".to_string(),
//         }))
//     }
// }

/// Start the gRPC server (simplified for testing)
pub async fn start_grpc_server(
    _addr: &str,
    _orchestrator: SyncOrchestrator,
) -> Result<(), Box<dyn std::error::Error>> {
    info!("gRPC server disabled for initial testing");
    info!("To enable gRPC:");
    info!("1. Install protoc (Protocol Buffers compiler)");
    info!("2. Uncomment build.rs to compile protocol.proto");
    info!("3. Uncomment grpc_server.rs to use generated code");
    Ok(())
}
