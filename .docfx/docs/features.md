# Features

- **BOLT Compatibility:** We follow the Basis of Lightning Technology (BOLT) specifications to maintain compatibility
  with other Lightning Network implementations.
- **Modular Design:** The implementation is designed with modularity in mind, making it easy to extend and customize.
- **Efficiency:** We strive for efficient and optimized code to ensure fast and reliable performance.
- **Community Support:** We welcome contributions and feedback from the community to improve and enhance the project.

## Current State of BOLT implementation

| BOLT                                                      | Library (API) | Full Node (daemon) |
|-----------------------------------------------------------|:-------------:|:------------------:|
| BOLT 1: Base Protocol                                     |       ✅       |         ❌          |
| BOLT 2: Peer Protocol for Channel Management              |       ✅       |         ❌          |
| BOLT 3: Bitcoin Transaction and Script Formats            |       ✅       |         ❌          |
| BOLT 4: Onion Routing Protocol                            |       ❌       |         ❌          |
| BOLT 5: Recommendations for On-chain Transaction Handling |       ❌       |         ❌          |
| BOLT 7: P2P Node and Channel Discovery                    |       ❌       |         ❌          |
| BOLT 8: Encrypted and Authenticated Transport             |       ✅       |         ✅          |
| BOLT 9: Assigned Feature Flags                            |       ✅       |         ✅          |
| BOLT 10: DNS Bootstrap and Assisted Node Location         |       ✅       |         ❌          |
| BOLT 11: Invoice Protocol for Lightning Payments          |       ✅       |         ✅          |
