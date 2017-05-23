# PanTiltServoDemo
An example ViewModel &amp; UI to control a Pan/Tilt behavior of homemade servo brackets. Utilizes a MVVM code pattern with Prism.

The UI:
[[https://github.com/au42/PanTiltServoDemo/blob/master/docs/img/UIDemo.gif|alt=UIDemo]]

A Pan/Tilt Servo Bracket being controlled by this app:
[[https://github.com/au42/PanTiltServoDemo/blob/master/docs/img/CameraDemo.GIF|alt=CameraDemo]]

## Project Summary
The initial release of the ServoDemo files contians three projects:

 - **PanTiltServoVM** - A full ViewModel built to support a Pan/Tilt assembly using most features of the Pololu Maestro servo controller. Uses Prism properties and throws Movement events when enabled.

 - **WPFApp** - An example View including a 2d-graph control with direct mouse click & drag control of the connected servo. Built with WPF and VM dependency injection.

 - **MovementTest** - Unit tests of PanTiltServoVM where applicable.

 ## Design Features
The Pololu Maestro C# driver requires a lower-level USB call in order to fetch all properties of the driven servos. This project aims to simplify the translation from these drivers into a usable and practical app with MVVM considerations in mind. 

This example includes the following features to build upon:
 
 - Simple getting/setting of movement/speed/accel properties with background management of device connection and cleanup (implements IDisposable)

 - Internal Task management of optional polling for live updates of movement/speed/accel properties to calling processes / UI

 - UI with 2D grid control of Pan/Tilt for easy debugging and testing of physical unit builds

 - Open-loop feedback of position negates the need for additional hardware (under certain conditions)

 - Position "goal"/"actual" properties reduce confusion of how the Pololu reports its position to the SDK

 - Automatic default connection of connected modules (with optional override for serial string and direct instance injection)

## Considerations

 - Build requires .NET 4.5.2, Prism Core and WPF, and Pololu USB DLLs

 - All position property feedback is open-loop and dependant on how the Pololu module returns its current servo data. It should not be relied on for anything with physical consequences

 - Position feedback will be "live" only when a realistic non-zero speed / accel value is set. Otherwise position updates will be 'instant' and not reflect real-world positions

 - WPFApp assumes defaults of 4000/8000 for min/max servo values, and channels 0/1 to Pan/Tilt respectively.