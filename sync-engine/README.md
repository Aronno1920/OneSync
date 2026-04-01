# OneSync Engine

High-performance file synchronization engine with block-level delta transfer, written in Rust.

## Prerequisites

### Windows

You need to install Visual Studio Build Tools with C++ support:

1. Download and install [Visual Studio Build Tools](https://visualstudio.microsoft.com/downloads/)
2. During installation, select **"Desktop development with C++"**
3. Make sure to include **MSVC v143 - VS 2022 C++ x64/x86 build tools**

Alternatively, you can use the [Visual Studio Installer](https://visualstudio.microsoft.com/vs/) to install the required components.

### Linux

```bash
sudo apt-get update
sudo apt-get install build-essential pkg-config libssl-dev
```

### macOS

```bash
xcode-select --install
```

## Building

1. **Install Rust** (if not already installed):
   ```bash
   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
   ```

2. **Build the project**:
   ```bash
   cd sync-engine
   cargo build --release
   ```

3. **Run checks**:
   ```bash
   cargo check
   cargo clippy
   ```

## Testing

Run the test suite:
```bash
cargo test
```

Run with output:
```bash
cargo test -- --nocapture
```

## Running

Start the sync engine server:
```bash
cargo run -- --addr 127.0.0.1:50051
```

Or run the release build:
```bash
./target/release/sync-engine --addr 127.0.0.1:50051
```

## Project Structure

```
sync-engine/
├── Cargo.toml              # Project dependencies and configuration
├── build.rs                # Build script for Protocol Buffers
├── src/
│   ├── main.rs            # Binary entry point
│   ├── lib.rs            # Library entry point
│   ├── ipc/              # Inter-Process Communication (gRPC)
│   ├── core/             # Core sync logic
│   ├── storage/           # Database and metadata storage
│   ├── network/           # Network transfer and protocols
│   └── models/           # Data models
└── src/ipc/
    └── protocol.proto     # gRPC protocol definition
```

## Features

- **Block-level delta transfer** - Only transfer changed blocks using rsync algorithm
- **Memory-mapped files** - Efficient file operations for large files
- **Parallel scanning** - Fast directory traversal with concurrent operations
- **SQLite database** - Persistent metadata and change journal
- **gRPC IPC** - Communication with .NET MAUI UI layer
- **Compression** - Optional Zstandard compression for transfers
- **Conflict resolution** - Multiple strategies for handling conflicts
- **Real-time monitoring** - File system watcher for change detection

## Development

### Code Style

Run the formatter:
```bash
cargo fmt
```

Check for issues:
```bash
cargo clippy -- -D warnings
```

### Documentation

Generate and open documentation:
```bash
cargo doc --open
```

## Troubleshooting

### "link.exe not found" (Windows)

This error means you don't have Visual Studio C++ Build Tools installed. See the Prerequisites section above.

### Protocol Buffers compilation issues

If you encounter issues with `tonic-build`, try:
```bash
cargo clean
cargo build
```

### Database errors

The project uses SQLite with bundled libraries. If you encounter database errors, ensure you have write permissions in the project directory.

## License

MIT OR Apache-2.0
