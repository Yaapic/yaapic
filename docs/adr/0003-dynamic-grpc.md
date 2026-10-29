# ADR 0003: Dynamic gRPC calls

Status: Proposed (confirmed in M7)

## Context

A gRPC client must call any service from a `.proto` file or server reflection, with no generated code, and show
messages as JSON.

## Options

1. **`Grpc.Net.Client` with byte-array marshallers and our own descriptor-driven protobuf wire codec.** Messages are
   encoded and decoded from `FileDescriptorSet` data directly.
2. **`Google.Protobuf` reflection (`MessageDescriptor`, `JsonFormatter`).** Least code.
3. **Shell out to `grpcurl`.** No AOT concern, but an external binary to ship and no streaming control.

## AOT risks

`Google.Protobuf` reflection-based descriptors and `JsonFormatter` use runtime reflection and are not trimming safe:
option 2 is rejected under the AOT rules. Option 1 uses no reflection; the cost is writing and testing the wire codec
and a `.proto` parser. Option 3 adds a shipped dependency.

## Decision

Option 1: `Grpc.Net.Client` transport with `Method<byte[], byte[]>` plus an in-house codec. The `.proto` parsing
approach is settled in M7.
