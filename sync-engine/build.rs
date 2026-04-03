fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Use vendored protoc binary
    let protoc = protoc_bin_vendored::protoc_bin_path().unwrap();
    std::env::set_var("PROTOC", protoc);
    
    // Generate tonic gRPC code
    tonic_build::configure()
        .build_server(true)
        .build_client(true)
        .compile(
            &["src/ipc/protocol.proto"],
            &["src/ipc"],
        )?;
    
    Ok(())
}
