# IsoTp.CANBus.Net
A professional .NET implementation of the **ISO 15765-2 (ISO-TP)** protocol. This library provides a robust transport layer for automotive diagnostic standards like UDS (ISO 14229) over CAN Bus.

## Features
* **Segmentation:** Automatically breaks down large messages into multiple CAN frames.
* **Flow Control:** Manages transmission pace using **SEPARATION_TIME_MIN** and **BLOCK_SIZE**.
* **Reassembly:** Reconstructs multiple incoming CAN frames back into a single diagnostic message.
* **Flexible Driver:** Easily integrates with any CAN hardware (Vector, PEAK-System, USB-CAN, etc.) via a simple callback mechanism.
---

## Installation
```bash
dotnet add package IsoTp.CANBus.Net
```

## Quick Start
### 1. Initialize CAN Driver
The `ICanDriver` acts as a bridge between this library and your hardware.

```csharp
using IsoTp.CANBus.Net;

var canDriver = new ICanDriver();

// Link your hardware-specific send method here
canDriver.SendCanMessage = (id, data, length) => {
    // Call your CAN hardware API (e.g., PCAN, Vector, socketcan, etc.)
    // YourHardware.Write(id, data);
};
```
To receive messages, you must invoke the hardware bridge.

```csharp
// Inside your hardware receive loop
public void OnCanFrameArrived(uint id, byte[] data)
{
    // Important: This triggers the ISO-TP reassembly logic
    canDriver.InvokeReceive(id, data);
}
```
### 2. Configure ISO-TP Parameters
Define the addressing scheme. Flow control settings (STmin, BS) have sensible defaults.

```csharp
// Example for a different ECU (e.g., Transmission Control Module)
// These IDs are just examples; use the ones specific to your project.
uint myRequestID  = 0x712; // Example: Physical Request ID
uint myResponseID = 0x71A; // Example: Physical Response ID
uint myFunctionID = 0x7DF; // Example: Functional Broadcast ID (Optional)

// Initialize with your custom IDs
var tpParams = new IsoTpParams(myRequestID, myResponseID, myFunctionID);

// Optional: Override defaults if needed
// tpParams.SEPARATION_TIME_MIN = 10; // 10 ms
// tpParams.BLOCK_SIZE = 8;           // 8 frames per block
```

### 3. Setup Processor and Listen for Messages
The IsoTpProcessor handles all the complex logic of the protocol.

```csharp
var processor = new IsoTpProcessor(tpParams, canDriver);

// Subscribe to reassembled messages
processor.OnMessageReceived += (id, data) => {
    Console.WriteLine($"Received full message from ID {id:X}: {BitConverter.ToString(data)}");
};
```

### 4. Sending Data
To send a large diagnostic request, the processor will handle the segmentation automatically.

```csharp
byte[] requestData = new byte[] { 0x22, 0xF1, 0x90 }; 
processor.SendData(requestData);

// When hardware receives a frame, push it into the driver
// canDriver.InvokeReceive(0x7E8, new byte[] { ... });
```
---

## Key Parameters Explained
| Parameter | ISO-TP Term | Unit | Description |
| :--- | :--- | :--- | :--- |
| **`SEPARATION_TIME_MIN`** | STmin | Milliseconds | The minimum time wait between two Consecutive Frames (CF). Helps prevent overloading the receiver. |
| **`BLOCK_SIZE`** | BS | Frames | The number of Consecutive Frames (CF) allowed to be sent in a single block before waiting for a Flow Control confirm. |

> **Note:** Setting `BLOCK_SIZE = 0` means the sender can transmit all remaining frames without waiting for further Flow Control.

---
## License
Licensed under the MIT License.