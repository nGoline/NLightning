# Changelog

All notable changes to this project will be documented in this file.

## v1.0.0

Major release adding the full channel-opening flow, peer management, and a complete channel handler suite.

### Added

- Added `ChannelManager` to orchestrate channel lifecycle and persistence;
- Added channel message handlers: `AcceptChannel1MessageHandler`, `OpenChannel1MessageHandler`,
  `FundingCreatedMessageHandler`, `FundingSignedMessageHandler`, `FundingConfirmedMessageHandler`,
  `ChannelReadyMessageHandler`;
- Added `IChannelMessageHandler<T>` interface for typed channel message handling;
- Added `PeerManager` for managing peer connections, reconnection, and disconnection behavior;
- Added `DependencyInjection` extension class for convenient service registration;
- Added dependencies on `NLightning.Infrastructure` and `NLightning.Infrastructure.Bitcoin`;
- Added `Microsoft.Extensions.Logging` package reference;

### Changed

- Moved `MessageFactory` from `NLightning.Application.Factories` to `NLightning.Application.Protocol.Factories`;
- Refactored `AcceptChannel1MessageHandler` to comply with `ChannelTypeTlv` and use `payload.ToSelfDelay` for
  `ChannelConfig` creation;
- Refactored `OpenChannel1MessageHandler` to reflect latest BOLT feature flag
  changes ([lightning/bolts#1310](https://github.com/lightning/bolts/pull/1310)) and refine channel opening validation;
- Improved peer exception handling and error messaging in `PeerManager`;

### Fixed

- Fixed `ChannelConfig` creation when initiating a channel to use `payload.ToSelfDelay` instead of
  `tempChannel.ChannelConfig.ToSelfDelay`;
- Fixed MessagePack serialization warnings in `PeerManager`;

### Removed

- Removed stub `LightningKeyManager` (was fully commented out);

### Breaking Changes

- Dropped `net8.0` and `net9.0` targets; the library now requires **.NET 10.0** or later;
- `MessageFactory` namespace changed from `NLightning.Application.Factories` to
  `NLightning.Application.Protocol.Factories`;

## v0.0.1

Initial release