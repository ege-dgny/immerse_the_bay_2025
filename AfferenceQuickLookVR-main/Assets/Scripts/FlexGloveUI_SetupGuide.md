# Flex Glove UI Setup Guide

## Overview
This guide will help you set up a world space UI to display flex sensor data from your ESP32-connected glove in your Meta Quest VR app.

## Two Setup Options

### Option A: Head-Following UI (Recommended for VR)
The UI follows your head movement, always visible in front of you.

### Option B: Fixed World Space UI
The UI stays in a fixed position in the world.

---

## Option A: Head-Following UI Setup

### 1. Create Canvas as Child of Camera Rig
1. In Unity Hierarchy, expand **`[BuildingBlock] Camera Rig`** → **`TrackingSpace`**
2. Right-click on **`CenterEyeAnchor`** → **UI** → **Canvas**
3. Select the Canvas in the Hierarchy
4. In the Inspector:
   - **Render Mode**: "World Space"
   - **Position**: X=0, Y=0, Z=0.5 (places it 0.5m in front of your face)
   - **Scale**: X=0.001, Y=0.001, Z=0.001 (adjust to make text readable)
   - **Rotation**: X=0, Y=0, Z=0

### 2. Make UI Face Forward (Optional but Recommended)
1. Select the Canvas GameObject
2. Add Component → **VRBillboard** script
3. This will make the UI always face you as you turn your head

### 3. Add UI Elements
1. Right-click on the Canvas → **UI** → **Text - TextMeshPro** (create 5 text elements)
   - If prompted, click "Import TMP Essentials"
2. Name them: "ThumbText", "IndexText", "MiddleText", "RingText", "PinkyText"
3. Arrange them vertically (use Vertical Layout Group for easy arrangement)
4. Style the text (font size ~24-36, white color, bold)

### 4. Add the UI Display Script
1. Create an empty GameObject as a child of the Canvas (or attach directly to Canvas)
2. Name it "FlexGloveUI"
3. Add the **FlexGloveUIDisplay** component
4. Assign references:
   - Drag **FlexGloveManager** (or GameObject with FlexGloveSerialReader) into "Serial Reader"
   - Drag each TextMeshPro text element into corresponding fields

---

## Option B: Fixed World Space UI Setup

### 1. Create a World Space Canvas
1. In Unity, right-click in the Hierarchy → **UI** → **Canvas**
2. Select the Canvas in the Hierarchy
3. In the Inspector, change **Render Mode** from "Screen Space - Overlay" to **"World Space"**
4. Set the Canvas position (e.g., Position: X=0, Y=1.5, Z=2) - this places it in front of the user
5. Set the Canvas Scale (e.g., Scale: X=0.001, Y=0.001, Z=0.001) - adjust this to make the UI readable size

### 2. Add UI Elements
1. Right-click on the Canvas → **UI** → **Text - TextMeshPro** (create 5 text elements, one for each finger)
   - If prompted, click "Import TMP Essentials" to set up TextMeshPro
2. Name them: "ThumbText", "IndexText", "MiddleText", "RingText", "PinkyText"
3. Arrange them vertically or horizontally as desired
4. Style the text (font size, color, etc.) in the Inspector

### 3. Add the UI Display Script
1. Create an empty GameObject as a child of the Canvas (or attach directly to Canvas)
2. Name it "FlexGloveUI"
3. Add the **FlexGloveUIDisplay** component to this GameObject
4. In the Inspector:
   - Drag your **FlexGloveSerialReader** GameObject into the "Serial Reader" field
   - Drag each TextMeshPro text element into the corresponding field:
     - Thumb Text → thumbText
     - Index Text → indexText
     - Middle Text → middleText
     - Ring Text → ringText
     - Pinky Text → pinkyText

### 4. Optional: Add Visual Bars
For a more visual representation, you can add Image components as progress bars:
1. For each finger, add an Image GameObject as a child
2. Set Image Type to "Filled"
3. Modify the FlexGloveUIDisplay script to also update fill amounts based on sensor values

### 5. Test
1. Make sure your ESP32 is connected and the FlexGloveSerialReader is working
2. Enter Play Mode
3. The UI should update in real-time showing the sensor values

## Tips
- Adjust Canvas Scale to make text readable in VR (typically 0.001 to 0.01)
- Position the Canvas where it's comfortable to view (usually 1-3 meters in front)
- Consider making the Canvas face the camera rig for better visibility
- You can add a background panel for better contrast

