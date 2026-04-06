# Changelog

All notable changes to this project will be documented in this file.

## v2.0.0

Peer service improvements, warning message propagation, `ChannelIdFactory` extension, and transport hardening.

### Added

- Added `PeerCommunicationService.SendWarningAsync` to send `WarningMessage` to peers on disconnect;
- Added `PeerService.SendWarningAsync` and `OnAttentionMessageReceived`/`OnExceptionRaised` events;
- Added `ChannelIdFactory.CreateTemporaryChannelId` for generating random temporary channel IDs;
- Added `ITcpService.ListeningTo` property exposing active listen endpoints;
- Added `host` and `port` parameters to `ConnectedPeer` constructor;

### Changed

- `PeerCommunicationService.DisconnectEvent` changed from `EventHandler?` to `EventHandler<Exception?>?`;
- `PeerCommunicationService.Disconnect` and `PeerService.Disconnect` now accept an optional `Exception` parameter and send an error/warning message before closing;
- `ITcpService.ConnectToPeerAsync` parameter changed from `PeerAddressInfo` to `PeerAddress`;
- Updated `libsodium` to `v1.0.21`;
- Updated `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.Options` to `v10.0.5`;
- Updated `PackageProjectUrl` to `https://docs.nlightn.ing`;

### Breaking Changes

- Dropped `net8.0` and `net9.0` targets; the library now requires **.NET 10.0** or later;
- `PeerCommunicationService.DisconnectEvent` signature changed from `EventHandler?` to `EventHandler<Exception?>?`;
- `ITcpService.ConnectToPeerAsync` now takes `PeerAddress` instead of `PeerAddressInfo`;
- `ConnectedPeer` constructor now requires `host` and `port` parameters;

## v1.0.3

Fixed an error on `SodiumJsCryptoProvider` where Blazor apps whould be unable to call `Sha256.GetHashAndReset`.

### Fixed

- Fixed `SodiumJsCryptoProvider.Sha256Final` to pass the arguments as `begin`, `end` to
  `LibsodiumJsWrapper.HEAPU8_subarray` instead of `begin`, `length`;
- Fixed `LibsodiumJsWrapper.HEAPU8_subarray` argument name so it's clear it expects `begin` and `end` instead of `begin`
  and `lenght`.

## v1.0.2

Bump version to use `NLightning.Domain@v1.1.2`.

## v1.0.1

Bump version to use `NLightning.Domain@v1.1.1`.

## v1.0.0

This version introduces a comprehensive refactoring of the `NLightning.Infrastructure` layer, focusing on enhancing
peer-to-peer networking, TCP transport capabilities, and cryptographic services.

Key changes include the introduction of new services, significant API modifications, and updates to dependencies, aiming
for improved modularity and robustness.

### Added

- Implemented `TcpService` and its interface `ITcpService` for handling TCP connections;
- Added `ConnectedPeer` value object to represent active peer connections;
- Added `NewPeerConnectedEventArgs` for transport-layer new peer connection events;
- Added `RemoteAddressTlvConverter` for serializing and deserializing remote peer addresses in TLV streams;
- Added `KeyFileData` model for structured storage of node key information;
- Introduced `Role` enum (`Initiator`, `Responder`) to provide clarity in the handshake process.
- Added implicit operators to `SecureMemory` for `Span<byte>` and `ReadOnlySpan<byte>` conversion;
- Added targets for bundling `LibsodiumJs` and its dependencies into `blazorSodium.bundle.js` for
  `NLightning.Infrastructure.Blazor`.

### Modified

- Split `Peer` into `PeerService` and `PeerCommunicationService` to better separate the roles of managing the peer
  lifecycle and communication.
- Renamed `PeerFactory` to `PeerServiceFactory` to reflect changes on `Peer`;
- Moved `ChannelIdFactory` from `Factories` to `Protocol.Factories` for better organization of protocol-related
  factories;
- Constructors and methods within handshake components (`HandshakePattern`, `PreMessagePattern`, `HandshakeState`,
  `HandshakeService`) were adjusted to align with protocol updates.
- The `PubKey` property in `PeerAddress` has been modified to use domain specific type `CompactPubKey`;
- Changed signatures for cryptographic methods in `NativeCryptoProvider`, including `AeadXChaCha20Poly1305IetfEncrypt`,
  `AeadXChaCha20Poly1305IetfDecrypt`, and `DeriveKeyFromPasswordUsingArgon2I`;
- Methods `InsertSecret` and `DeriveOldSecret` in `SecretStorageService` now uses domain specific type `Secret`;
- Internal representation or access methods for string constants in `ProtocolConstants.cs` were updated.

### Breaking Changes

- The legacy peer management system, including `Peer.cs`, `IPeer.cs`, `PeerFactory.cs`, `IPeerFactory.cs`,
  `PeerManager.cs`, and `IPeerManager.cs`, has been removed. These are superseded by the new `PeerService`
  architecture;
- Moved core cryptographic classes: `NLightningContext.cs`, the original `Ecdh.cs` implementation, `Ripemd160.cs`,
  and `KeyPair.cs` to `NLightning.Infrastructure.Bitcoin`;
- Moved utility class `Bech32Encoder` to `NLightning.Infrastructure.Bitcoin.Encoders`;
- The previous `ChannelIdFactory.cs` was deleted and replaced by a new implementation in
  `NLightning.Infrastructure.Protocol.Factories`.
- Removed `PingPongServiceFactory.cs`.
- Removed data model `CommitmentNumber.cs`.
- Moved `KeyDerivationService.cs` to `NLightning.Infrastructure.Bitcoin.Services`;
- As detailed in the "Modified" section, changes to method signatures in `IEcdh`, `SecureMemory`,
  `NativeCryptoProvider`, and `SecretStorageService` are breaking;
- The modification to the `PubKey` property in `PeerAddress` constitutes a breaking change.
- Removed direct dependencies on `NBitcoin` and `NBitcoin.Secp256k1`. Projects consuming `NLightning.Infrastructure`
  that relied on these packages transitively will need to add direct references or adapt to any new abstractions
  provided.

## v0.0.2

Disabled the `Build Compression` for the `*.Wasm` target. This was causing issues with the `dotnet publish` command,
which was not able to resolve the `*.br` files.

### Added

- Added `<DisableBuildCompression>true</DisableBuildCompression>` to the `*.Wasm` target in the project file.

## v0.0.1

Initial release