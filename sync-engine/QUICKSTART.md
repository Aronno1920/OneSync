# Quick Start Guide - Testing Sync Engine

This guide will help you verify that the sync-engine project is set up correctly and working.

## Step 1: Install Prerequisites

### For Windows Users (Required!)

You **must** install Visual Studio Build Tools with C++ support:

1. Download [Visual Studio Build Tools](https://visualstudio.microsoft.com/downloads/)
2. Run the installer
3. Select **"Desktop development with C++"**
4. Make sure these components are checked:
   - **MSVC v143 - VS 2022 C++ x64/x86 build tools**
   - **Windows 10/11 SDK** (latest version)
5. Click **Install**

**Important**: VS Code is NOT sufficient - you need the full Build Tools with C++ compiler.

### Verify Rust Installation

```bash
rustc --version
cargo --version
```

If Rust is not installed:
```bash
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
```

## Step 2: Basic Syntax Check

Check if the code compiles (without building):

```bash
cd sync-engine
cargo check
```

**Expected output**: No errors (warnings are okay)

If you see "link.exe not found", go back to Step 1.

## Step 3: Run Unit Tests

Test the core data models:

```bash
cargo test --lib
```

**Expected output**: All tests pass

```
running 6 tests
test models::file_node::tests::test_file_node_creation ... ok
test models::file_node::tests::test_directory_node ... ok
test models::file_node::tests::test_flatten_files ... ok
test models::file_node::tests::test_find_file ... ok
test models::sync_job::tests::test_sync_job_creation ... ok
test models::sync_job::tests::test_conflict_resolution ... ok
test models::sync_job::tests::test_progress_percentage ... ok

test result: ok. 7 passed; 0 failed; 0 ignored
```

## Step 4: Run Integration Tests

Test the integration points:

```bash
cargo test --test integration_test
```

**Expected output**: All tests pass

```
running 6 tests
test test_file_node_creation ... ok
test test_directory_node ... ok
test test_sync_job_creation ... ok
test test_flatten_files ... ok
test test_find_file ... ok
test test_total_size ... ok

test result: ok. 6 passed; 0 failed; 0 ignored
```

## Step 5: Build the Project

Build the complete project:

```bash
cargo build
```

**Expected output**: Successful compilation

```
   Compiling sync-engine v0.1.0 (f:\Project\OneSync\sync-engine)
    Finished dev [unoptimized + debuginfo] target(s) in X.XXs
```

## Step 6: Run Linter (Optional but Recommended)

Check for code quality issues:

```bash
cargo clippy
```

Fix any warnings or errors that appear.

## Step 7: Format Code (Optional)

Ensure consistent code formatting:

```bash
cargo fmt
```

## Step 8: Build Release Version

Build optimized release binary:

```bash
cargo build --release
```

The binary will be at: `target/release/sync-engine.exe`

## Step 9: Test the Binary

Run the sync engine:

```bash
./target/release/sync-engine.exe --help
```

**Expected output**: Help message with usage options

```
OneSync high-performance file synchronization engine

Usage: sync-engine.exe [OPTIONS]

Options:
  -a, --addr <ADDR>  gRPC server address [default: 127.0.0.1:50051]
  -v, --verbose      Enable verbose logging
  -h, --help         Print help
```

## Step 10: Start the Server

Start the sync engine server:

```bash
cargo run -- --addr 127.0.0.1:50051
```

**Expected output**: Server starts successfully

```
2024-04-01T11:43:01.074Z INFO sync_engine: Starting OneSync Engine v0.1.0
2024-04-01T11:43:01.074Z INFO sync_engine::core::orchestrator: Initializing sync orchestrator
2024-04-01T11:43:01.074Z INFO sync_engine::storage::database: Opening database: sync_engine.db
2024-04-01T11:43:01.074Z INFO sync_engine::storage::database: Database initialized successfully
2024-04-01T11:43:01.074Z INFO sync_engine::storage::metadata: Initializing metadata store
2024-04-01T11:43:01.074Z INFO sync_engine::storage::journal: Initializing change journal
2024-04-01T11:43:01.074Z INFO sync_engine::ipc::grpc_server: Starting gRPC server on 127.0.0.1:50051
```

Press `Ctrl+C` to stop the server.

## Troubleshooting

### Error: "link.exe not found"

**Solution**: Install Visual Studio Build Tools with C++ (see Step 1)

### Error: "protocol.proto" compilation fails

**Solution**: 
```bash
cargo clean
cargo build
```

### Error: Database permission denied

**Solution**: Ensure you have write permissions in the project directory

### Error: "tonic-build" not found

**Solution**: The build.rs file handles this automatically. If issues persist:
```bash
cargo clean
cargo build
```

## Success Indicators

Your sync-engine is working correctly if:

✅ `cargo check` completes without errors
✅ `cargo test` shows all tests passing
✅ `cargo build` compiles successfully
✅ `cargo run -- --help` shows usage information
✅ Server starts and shows initialization messages

## Next Steps

Once basic testing is complete:

1. Review the code structure in `src/`
2. Read the main documentation: `README.md`
3. Explore individual modules:
   - `src/core/` - Core synchronization logic
   - `src/network/` - Network transfer
   - `src/storage/` - Database and metadata
   - `src/ipc/` - gRPC communication
   - `src/models/` - Data structures

## Getting Help

If you encounter issues:

1. Check the [README.md](README.md) for detailed documentation
2. Review error messages carefully
3. Ensure all prerequisites are installed
4. Try `cargo clean && cargo build`
5. Check Rust version: `rustc --version` (should be 1.70+)

## Performance Tips

For production builds:

```bash
cargo build --release
```

This enables optimizations and creates a much faster binary.
