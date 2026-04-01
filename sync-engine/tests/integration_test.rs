//! Integration tests for sync engine
//!
//! These tests verify the basic functionality of the sync engine components.

use sync_engine::models::file_node::FileNode;
use sync_engine::models::sync_job::{SyncJob, SyncJobConfig, SyncDirection, ConflictStrategy};

#[test]
fn test_file_node_creation() {
    let node = FileNode::new("/test/file.txt".to_string(), false);
    assert_eq!(node.name(), "file.txt");
    assert_eq!(node.parent(), Some("/test"));
    assert_eq!(node.file_count(), 1);
    assert!(!node.is_directory);
}

#[test]
fn test_directory_node() {
    let mut dir = FileNode::new("/test".to_string(), true);
    dir.children.push(FileNode::new("/test/file1.txt".to_string(), false));
    dir.children.push(FileNode::new("/test/file2.txt".to_string(), false));

    assert_eq!(dir.file_count(), 2);
    assert_eq!(dir.name(), "test");
    assert!(dir.is_directory);
}

#[test]
fn test_sync_job_creation() {
    let config = SyncJobConfig {
        source_path: "/source".to_string(),
        target_path: "/target".to_string(),
        direction: SyncDirection::Bidirectional,
        conflict_strategy: ConflictStrategy::LastWriteWins,
        ..Default::default()
    };

    let job = SyncJob::with_config(config);
    assert_eq!(job.status, sync_engine::models::sync_job::SyncStatus::Idle);
    assert!(!job.is_running());
    assert!(!job.is_completed());
}

#[test]
fn test_flatten_files() {
    let mut dir = FileNode::new("/test".to_string(), true);
    dir.children.push(FileNode::new("/test/file1.txt".to_string(), false));
    dir.children.push(FileNode::new("/test/file2.txt".to_string(), false));

    let files = dir.flatten_files();
    assert_eq!(files.len(), 2);
}

#[test]
fn test_find_file() {
    let mut dir = FileNode::new("/test".to_string(), true);
    dir.children.push(FileNode::new("/test/file1.txt".to_string(), false));

    let found = dir.find_file("/test/file1.txt");
    assert!(found.is_some());
    assert_eq!(found.unwrap().path, "/test/file1.txt");
}

#[test]
fn test_total_size() {
    let mut dir = FileNode::new("/test".to_string(), true);
    
    let mut file1 = FileNode::new("/test/file1.txt".to_string(), false);
    file1.size = 100;
    dir.children.push(file1);
    
    let mut file2 = FileNode::new("/test/file2.txt".to_string(), false);
    file2.size = 200;
    dir.children.push(file2);

    assert_eq!(dir.total_size(), 300);
}
