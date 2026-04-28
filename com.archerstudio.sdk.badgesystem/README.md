# ArcherStudio Badge Notification

**Badge Notification** (formerly Dot Badge Notification) is a high-performance, hierarchical red-dot notification system for Unity games. It utilizes a Trie data structure and Pub/Sub pattern to efficiently manage and propagate notifications across complex UI menus.

## Features

*   **Hierarchical Propagation:** Updating a child node (e.g., `Mail|System`) automatically updates parent nodes (`Mail` and `Root`).
*   **Zero Allocation Runtime:** Optimized using `Span<char>` and `ZString` to avoid garbage collection during updates.
*   **Decoupled Architecture:** UI logic is completely separated from badge logic via `IPubSub`.
*   **Graph Editor:** (Optional) Visual editor support via XNode to design badge trees.

## Installation

### Via Package Manager (Git)

1. Open `Window > Package Manager`.
2. Click `+` > `Add package from git URL`.
3. Enter: `https://github.com/felixngd/BadgeNotification.git?path=Packages/BadgeNotification`

### Dependencies
*   [UniTask](https://github.com/Cysharp/UniTask)
*   [XNode](https://github.com/Siccity/xNode) (Optional, for Graph Editor)

## Quick Start

```csharp
// 1. Initialize
IPubSub<BadgeChangedMessage<int>> messaging = new MessagePipeMessaging();
BadgeMessaging<int>.Initialize(messaging);

// 2. Setup System
var badgeSystem = new MyBadgeNotification(new List<string> { "Root|Mail", "Root|Quest" });

// 3. Update Badge
badgeSystem.UpdateBadgeCount("Root|Mail", 1);
```

For detailed documentation, please refer to the [Documentation](./Documentation~/index.md) folder.
