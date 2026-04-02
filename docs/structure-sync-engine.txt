sync-engine/
├── Cargo.toml
├── src/
│   ├── main.rs
│   ├── lib.rs
│   ├── ipc/
│   │   ├── mod.rs
│   │   ├── grpc_server.rs
│   │   └── protocol.proto
│   ├── core/
│   │   ├── mod.rs
│   │   ├── orchestrator.rs
│   │   ├── scanner.rs
│   │   ├── differ.rs
│   │   └── watcher.rs
│   ├── storage/
│   │   ├── mod.rs
│   │   ├── database.rs
│   │   ├── metadata.rs
│   │   └── journal.rs
│   ├── network/
│   │   ├── mod.rs
│   │   ├── transfer.rs
│   │   ├── rsync_algorithm.rs
│   │   └── protocols.rs
│   └── models/
│       ├── mod.rs
│       ├── file_node.rs
│       └── sync_job.rs