//! File node model - Represents a file or directory in the sync tree
//!
//! This module provides the FileNode structure which represents files and directories
//! in a hierarchical tree structure for synchronization.

use std::path::Path;
use serde::{Serialize, Deserialize};

/// File node - represents a file or directory
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileNode {
    /// File or directory path
    pub path: String,
    /// Whether this is a directory
    pub is_directory: bool,
    /// File size in bytes
    pub size: u64,
    /// Last modified time (Unix timestamp)
    pub modified_time: i64,
    /// BLAKE3 hash of file contents (empty for directories)
    pub hash: String,
    /// Child nodes (only for directories)
    pub children: Vec<FileNode>,
}

impl FileNode {
    /// Create a new file node
    pub fn new(path: String, is_directory: bool) -> Self {
        Self {
            path,
            is_directory,
            size: 0,
            modified_time: 0,
            hash: String::new(),
            children: Vec::new(),
        }
    }

    /// Get the file name from the path
    pub fn name(&self) -> &str {
        Path::new(&self.path)
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or("")
    }

    /// Get the parent directory path
    pub fn parent(&self) -> Option<&str> {
        Path::new(&self.path)
            .parent()
            .and_then(|p| p.to_str())
    }

    /// Count total number of files in this tree
    pub fn file_count(&self) -> usize {
        if self.is_directory {
            self.children.iter().map(|c| c.file_count()).sum()
        } else {
            1
        }
    }

    /// Get total size of all files in this tree
    pub fn total_size(&self) -> u64 {
        if self.is_directory {
            self.children.iter().map(|c| c.total_size()).sum()
        } else {
            self.size
        }
    }

    /// Flatten the tree into a list of all files
    pub fn flatten_files(&self) -> Vec<FileNode> {
        let mut files = Vec::new();

        if self.is_directory {
            for child in &self.children {
                files.extend(child.flatten_files());
            }
        } else {
            files.push(self.clone());
        }

        files
    }

    /// Find a file by path in the tree
    pub fn find_file(&self, path: &str) -> Option<FileNode> {
        if self.path == path {
            return Some(self.clone());
        }

        if self.is_directory {
            for child in &self.children {
                if let Some(found) = child.find_file(path) {
                    return Some(found);
                }
            }
        }

        None
    }

    /// Check if this node matches another node
    pub fn matches(&self, other: &FileNode) -> bool {
        self.path == other.path && self.hash == other.hash
    }

    /// Check if this node is newer than another node
    pub fn is_newer_than(&self, other: &FileNode) -> bool {
        self.modified_time > other.modified_time
    }

    /// Create a hash map of all files in the tree
    pub fn to_hash_map(&self) -> std::collections::HashMap<String, FileNode> {
        let mut map = std::collections::HashMap::new();

        if self.is_directory {
            for child in &self.children {
                map.extend(child.to_hash_map());
            }
        } else {
            map.insert(self.path.clone(), self.clone());
        }

        map
    }
}

impl PartialEq for FileNode {
    fn eq(&self, other: &Self) -> bool {
        self.path == other.path && self.hash == other.hash && self.size == other.size
    }
}

impl Eq for FileNode {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_file_node_creation() {
        let node = FileNode::new("/test/file.txt".to_string(), false);
        assert_eq!(node.name(), "file.txt");
        assert_eq!(node.parent(), Some("/test"));
        assert_eq!(node.file_count(), 1);
    }

    #[test]
    fn test_directory_node() {
        let mut dir = FileNode::new("/test".to_string(), true);
        dir.children.push(FileNode::new("/test/file1.txt".to_string(), false));
        dir.children.push(FileNode::new("/test/file2.txt".to_string(), false));

        assert_eq!(dir.file_count(), 2);
        assert_eq!(dir.name(), "test");
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
}
