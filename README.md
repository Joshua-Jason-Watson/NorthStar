# NorthStar

NorthStar is a C# real-time camera tracking project focused on pose and motion tracking.

## Current Status

NorthStar is currently under active development.

The project currently includes:

* Windows camera discovery and capture through Media Foundation
* Camera capability detection
* NV12 frame decoding and conversion
* Image preprocessing
* ONNX-based pose estimation
* RTMPose-M pose detection
* Tracking and coordinate processing
* A Windows UI for visualization

## Project Structure

* `NorthStar/` — Core camera, image processing, pose tracking, and pipeline code
* `NorthStar.UI/` — Windows user interface
* `NorthStar.slnx` — Solution file
* `NorthStar/Models/` — Machine-learning models used by NorthStar

## Requirements

* Windows
* .NET
* A compatible camera
* The included RTMPose-M model

## Model

NorthStar currently uses `rtmpose-m.onnx` for pose estimation.

The model is included in the repository so that a fresh clone contains the dependencies required to run the project.

## Development

NorthStar is experimental software and is currently being developed incrementally. APIs, project structure, and implementation details may change as development continues.
