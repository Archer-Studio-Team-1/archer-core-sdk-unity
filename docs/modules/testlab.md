# Module: com.archerstudio.sdk.testlab

Firebase Test Lab Game Loop integration. Detect TestLab launch, chạy scenario theo config, log result, signal completion.

**Version**: `1.0.0` · **Deps**: `core` · **Priority**: — (không phải ISDKModule chính, chạy standalone MonoBehaviour).

---

## 1. Public API

```csharp
GameLoopHandler.Instance.CompleteCurrentScenario(passed, message);
GameLoopHandler.Instance.IsRunning;
GameLoopHandler.Instance.GetResults();

GameLoopHandler.OnScenarioStarted   += n  => { /* scenario number */ };
GameLoopHandler.OnScenarioCompleted += r  => { /* GameLoopResult */ };

GameLoopDetector.IsRunningInTestLab;    // static
GameLoopDetector.ScenarioNumber;         // static
```

## 2. Cấu trúc (không dùng provider pattern)

Test Lab là integration đơn giản: detect launch intent / URL → chạy scenario → ghi result → exit. Không có vendor swap nên không cần `IXxxProvider`.

- `GameLoopDetector` (static) — check intent/URL khi Unity start.
- `GameLoopHandler` (MonoBehaviour, không ISDKModule) — orchestrate scenario, fire events.

## 3. Events

- `OnScenarioStarted(int scenarioNumber)` — static delegate.
- `OnScenarioCompleted(GameLoopResult)` — static delegate.
- `GameLoopResult` (serializable struct): `ScenarioNumber`, `ScenarioName`, `Passed`, `DurationSeconds`, `Message`, `Timestamp`.

## 4. Config: `TestLabConfig`

| Field | Ý nghĩa |
|---|---|
| `Scenarios` | `List<GameLoopScenarioEntry>` — mỗi entry: Name, Description, SceneName, Enabled. |
| `ScenarioTimeoutSeconds` | default 300. |
| Device defaults (gcloud) | model, orientation, locale — cho editor runner. |

## 5. Key Runtime files

| File | Vai trò |
|---|---|
| `Runtime/Core/GameLoopHandler.cs` | Scenario runner, result writer. |
| `Runtime/Core/GameLoopDetector.cs` | Detect Test Lab launch (Android intent / iOS URL scheme). |
| `Runtime/Config/TestLabConfig.cs` | SO + scenario entries. |
| `Runtime/Scenarios/BuiltInScenarios.cs` | Stock scenarios (smoke test, progression, ads flow). |

## 6. Platform hooks

### Android
- Intent action: `com.google.intent.action.TEST_LOOP`.
- Scenario number nằm trong `Intent.data.getQueryParameter("scenario")`.
- Kết quả ghi vào `/sdcard/data/local/tmp/game_loop_results/*.json`.

### iOS
- URL scheme: `firebase-game-loop://`.
- Scenario number từ query param.
- Kết quả ghi vào `Documents/game_loop_results/`.

## 7. Editor tooling

| File | Mục đích |
|---|---|
| `Editor/TestLabBuildProcessor.cs` | `IPreprocessBuildWithReport` + `IPostprocessBuildWithReport` — inject intent-filter (AndroidManifest) và `CFBundleURLSchemes` (Info.plist). |
| `Editor/TestLabWindow.cs` | EditorWindow: scenario CRUD, trigger gcloud CLI command. |

## 8. Chú ý khi sửa

- **Không dùng khi release**: module chỉ hoạt động trong Test Lab environment (detector false ở thiết bị thật).
- **Scenario timeout**: nếu scenario không gọi `CompleteCurrentScenario` trong `ScenarioTimeoutSeconds` → TestLab báo fail timeout.
- **Build processor** inject manifest phải chạy trước các processor khác (`callbackOrder` nhỏ).
- **Result path**: Firebase Test Lab tự ingest từ vị trí chuẩn; không đổi đường dẫn.
