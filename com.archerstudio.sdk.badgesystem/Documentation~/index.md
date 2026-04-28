# Badge Notification Documentation

## Overview

The **ArcherStudio Badge Notification** system provides a robust solution for managing "red dot" indicators in games. It excels in scenarios where notifications are nested (e.g., Main Menu > Inventory > Weapons > Sword).

## Architecture

The system is built on two core pillars:

1.  **Trie (Prefix Tree):** Used to store the badge hierarchy. This allows for $O(L)$ operations (where L is the key length) and efficient parent/child traversals.
2.  **Pub/Sub Pattern:** Decouples the "Data" layer (Trie) from the "View" layer (UI).

### Data Flow
1.  **Action:** Game logic triggers `badgeSystem.UpdateBadgeCount("Root|Mail", 1)`.
2.  **Processing:** 
    *   The Trie updates the specific node.
    *   The Trie walks up the tree, updating all parent nodes (Aggregation).
3.  **Notification:** `BadgeMessaging` publishes a `BadgeChangedMessage` for *every* node that changed.
4.  **UI Update:** Subscribers (UI Buttons) receive the message and toggle their visibility/text.

## Getting Started

### 1. Implementing the Messaging Adapter

You must provide an implementation of `IPubSub<T>`. This allows you to use any event system (UniRx, MessagePipe, C# Events).

```csharp
using ArcherStudio.Badge.Runtime.Interfaces;

public class MyMessaging : IPubSub<BadgeChangedMessage<int>>
{
    public void Publish(BadgeChangedMessage<int> msg) { /* Your Event Bus Publish */ }
    public IDisposable Subscribe(string key, Action<BadgeChangedMessage<int>> cb) { /* Your Event Bus Subscribe */ }
}
```

### 2. Extending the Badge System

Create a wrapper class for your specific game needs.

```csharp
using ArcherStudio.Badge.Runtime;

public class GameBadgeSystem : BadgeNotificationBase<int>
{
    public GameBadgeSystem(List<string> keys) : base()
    {
        _trieMap = new TrieMap<BadgeData<int>>();
        SetDefaultNodeData("Root");
        foreach(var key in keys)
        {
             // Initialize default values
             // ...
        }
    }
}
```

## API Reference

### `BadgeNotificationBase<TValue>`

| Method | Description |
| :--- | :--- |
| `AddBadge(string key, int value)` | Adds a new node to the Trie dynamically. |
| `UpdateBadgeCount(string key, int delta)` | Adds/Subtracts from the current count. Automatically propagates to parents. |
| `SetBadgeCount(string key, int count)` | Sets the absolute value of a badge. |
| `GetBadgeCount(string key)` | Returns the current integer count of a badge. |
| `GetBadgeValue(string key)` | Returns the custom data struct (`TValue`) stored in the badge. |

### `BadgeMessaging<TValue>`

Static entry point for the messaging system.

| Method | Description |
| :--- | :--- |
| `Initialize(IPubSub<BadgeChangedMessage<T>>)` | Must be called before using the system. |
| `UpdateBadge(...)` | Internal use. Triggers the publish event. |

## Advanced Usage

### Custom Payload Data
Instead of just an `int`, you can store complex data in a badge using the Generic `TValue`.

```csharp
public struct BadgeInfo 
{
    public Color Color;
    public string IconPath;
}

public class RichBadgeSystem : BadgeNotificationBase<BadgeInfo> { ... }
```

### Performance Notes
*   **Strings:** The system is optimized to handle string keys with minimal allocation using `ZString`.
*   **Keys:** Use `const` strings for keys to avoid typo bugs.
*   **Initialization:** Pre-populate your Trie with known keys at startup for best performance, though dynamic addition is supported.
