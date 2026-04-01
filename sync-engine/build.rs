fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Commented out for initial testing without Protocol Buffers
    // Uncomment after installing protoc:
    // tonic_build::configure()
    //     .build_server(true)
    //     .build_client(true)
    //     .compile(
    //         &["src/ipc/protocol.proto"],
    //         &["src/ipc"],
    //     )?;
    
    println!("cargo:warning=Protocol Buffers compilation disabled for initial testing");
    println!("cargo:warning=To enable gRPC, install protoc and uncomment build.rs");
    Ok(())
}
