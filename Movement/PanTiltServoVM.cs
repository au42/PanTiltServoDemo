using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Pololu.Usc;
using Pololu.UsbWrapper;

using Prism.Mvvm;

using PanTiltServoMVVM.ServoUtil;

namespace PanTiltServoMVVM
{
    /// <summary>
    /// A ViewModel sample application for a Pan/Tilt servo bracket utilizing the Pololu controller and driver base (which acts as the 'Model')
    /// </summary>
    public class PanTiltServoVM : BindableBase, IDisposable
    {

        #region Events

        //TODO: Add event args to each event with channel/value tied to them

        /// <summary>
        /// Fires when a Tilt movement is initiated
        /// </summary>
        public event EventHandler MovementStarted;

        /// <summary>
        /// Fires when Pan movement reaches goal.
        /// REQUIRES: USB Polling active and a non-zero Speed or Accel value set
        /// </summary>
        public event EventHandler MovementEnded;

        #endregion


        #region Private variables

        // The connected USB device (our "Model" for MVVM)
        private Usc _connectedController;

        // Servos of 'questionable quality' have some deadzones in their settings,
        // This value was found through trials to get best performance. 
        // NOTE: Make this a public property when we figure out events and polling
        private readonly ushort FUZZY_GOAL_RANGE = 45; 

        #endregion


        #region Public MetaProperties

        private string _activeSerial;
        /// <summary>
        /// The currently connected module's Serial/UID
        /// </summary>
        public string ActiveDeviceSerial
        {
            get { return _activeSerial; }
            private set { SetProperty(ref _activeSerial, value); }
        }

        private bool _isActive;
        /// <summary>
        /// True if a valid module is connected and able to accept movement commands
        /// </summary>
        public bool IsActive
        {
            get { return _isActive; }
            private set { SetProperty(ref _isActive, value); }
        }

        private bool _isPolling;
        /// <summary>
        /// True if there is a background Thread continuously checking Pan/Tilt Pos/Speed/Accel as reported by the Pololu hardware
        /// </summary>
        /// <remarks> Remember this is an open-loop measurement!</remarks>
        public bool IsPolling
        {
            get { return _isPolling; }
            private set { SetProperty(ref _isPolling, value); }
        }

        private MovementType _moving;
        /// <summary>
        /// True if the Pololu is actively changing the position value
        /// </summary>
        /// <remarks>
        /// REQUIRES: Non-zero Speed or Acceleration value to be of practical use (otherwise movement will be "instant" from module POV)
        /// </remarks>
        public MovementType Moving
        {
            get { return _moving; }
            set { SetProperty(ref _moving, value); }
        }


        private bool _enableEvents;
        /// <summary>
        /// True if Movement start/stop events will be thrown upon non-zero speed/accel
        /// </summary>
        public bool EventsEnabled
        {
            get { return _enableEvents; }
            set { SetProperty(ref _enableEvents, value); }
        }

        private ushort _minPanLimit = 4000;
        /// <summary>
        /// Minimum Pan value of controller (default 4000)
        /// </summary>
        public ushort MinPanLimit
        {
            get { return _minPanLimit; }
            set { SetProperty(ref _minPanLimit, value); }
        }

        private ushort _maxPanLimit = 8000;
        /// <summary>
        /// Maximum Pan value of controller (default 8000)
        /// </summary>
        public ushort MaxPanLimit
        {
            get { return _maxPanLimit; }
            set { SetProperty(ref _maxPanLimit, value); }
        }

        private ushort _minTiltLimit = 4000;
        /// <summary>
        /// Minimum Tilt value of controller (default 4000)
        /// </summary>
        public ushort MinTiltLimit
        {
            get { return _minTiltLimit; }
            set { SetProperty(ref _minTiltLimit, value); }
        }

        private ushort _maxTiltLimit = 8000;
        /// <summary>
        /// Maximum Tilt value of controller (default 8000)
        /// </summary>
        public ushort MaxTiltLimit
        {
            get { return _maxTiltLimit; }
            set { SetProperty(ref _maxTiltLimit, value); }
        }

        private byte _panServoChannel = 0;
        /// <summary>
        /// Byte value of the servo channel assigned to the 'Pan' servo (default 0)
        /// </summary>
        public byte PanServoChannel
        {
            get { return _panServoChannel; }
            set { SetProperty(ref _panServoChannel, value); }
        }

        private byte _tiltServoChannel = 1;
        /// <summary>
        /// Byte value of the servo channel assigned to the 'Tilt' servo (default 1)
        /// </summary>
        public byte TiltServoChannel
        {
            get { return _tiltServoChannel; }
            set { SetProperty(ref _tiltServoChannel, value); }
        }

        #endregion


        #region Public Pan/Tilt Properties

        private ushort _panPosGoal;
        /// <summary>
        /// Sets the goal position of the Pan servo to move to at the specified speed/accel
        /// </summary>
        public ushort PanPosGoal
        {
            get { return _panPosGoal; }
            set
            {
                SetProperty(ref _panPosGoal, value);
                SetPanPosition(value);
                if (!IsPolling)
                {
                    RaisePropertyChanged(nameof(PanPercentActual));
                    RaisePropertyChanged(nameof(PanPosActual));
                }
            }
        }

        private ushort _panPosActual;
        /// <summary>
        /// Gets the current position the Pololu controller is sending to the servo.
        /// </summary>
        /// <remarks>
        /// This is an open-loop measurement relying on the Pololu controller reports. Not 1:1 IRL
        /// </remarks>
        public ushort PanPosActual
        {
            get
            {
                if (!IsPolling) { return GetPanPos(); }
                else return _panPosActual;
            }
            private set { SetProperty(ref _panPosActual, value); }
        }

        private ushort _tiltPosGoal;
        /// <summary>
        /// Sets the goal position of the Tilt servo to move to at the specified speed/accel
        /// </summary>
        public ushort TiltPosGoal
        {
            get { return _tiltPosGoal; }
            set
            {
                SetProperty(ref _tiltPosGoal, value);
                SetTiltPosition(value);
                if (!IsPolling)
                {
                    RaisePropertyChanged(nameof(TiltPercentActual));
                    RaisePropertyChanged(nameof(TiltPosActual));
                }
            }
        }

        private ushort _tiltPosActual;
        /// <summary>
        /// Gets the current position the Pololu controller is sending to the servo.
        /// </summary>
        /// <remarks>
        /// This is an open-loop measurement relying on the Pololu controller reports. Not 1:1 IRL
        /// </remarks>
        public ushort TiltPosActual
        {
            get
            {
                if (!IsPolling) { return GetTiltPos(); }
                else return _tiltPosActual;
            }
            private set { SetProperty(ref _tiltPosActual, value); }
        }

        private int _panPercentGoal;
        /// <summary>
        /// Sets the goal position of the Pan servo (in percent) to move to at the specified speed/accel
        /// </summary>
        public int PanPercentGoal
        {
            get { return _panPercentGoal; }
            set
            {
                SetProperty(ref _panPercentGoal, value);
                double pos = MinPanLimit + ((value / 100.0) * (MaxPanLimit - MinPanLimit));
                SetPanPosition((ushort)Math.Round(pos));
                if (!IsPolling)
                {
                    RaisePropertyChanged(nameof(PanPercentActual));
                    RaisePropertyChanged(nameof(PanPosActual));
                }
            }
        }

        private int _panPercentActual;
        /// <summary>
        /// Gets the current position the Pololu controller is sending to the servo.
        /// </summary>
        /// <remarks>
        /// This is an open-loop measurement relying on the Pololu controller reports. Not 1:1 IRL
        /// </remarks>
        public int PanPercentActual
        {
            get
            {
                if (!IsPolling) { return 100 * (PanPosActual - MinPanLimit) / (MaxPanLimit - MinPanLimit); }
                else { return _panPercentActual; }
            }
            private set { SetProperty(ref _panPercentActual, value); }
        }

        private int _tiltPercentGoal;
        /// <summary>
        /// Sets the goal position of the Pan servo (in percent) to move to at the specified speed/accel
        /// </summary>
        public int TiltPercentGoal
        {
            get { return _tiltPercentGoal; }
            set
            {
                SetProperty(ref _tiltPercentGoal, value);
                double pos = MinTiltLimit + ((value / 100.0) * (MaxTiltLimit - MinTiltLimit));
                SetTiltPosition((ushort)Math.Round(pos));
                if (!IsPolling)
                {
                    RaisePropertyChanged(nameof(TiltPercentActual));
                    RaisePropertyChanged(nameof(TiltPosActual));
                }
            }
        }

        private int _tiltPercentActual;
        /// <summary>
        /// Gets the current position the Pololu controller is sending to the servo.
        /// </summary>
        /// <remarks>
        /// This is an open-loop measurement relying on the Pololu controller reports. Not 1:1 IRL
        /// </remarks>
        public int TiltPercentActual
        {
            get
            {
                if (!IsPolling) { return 100 * (TiltPosActual - MinTiltLimit) / (MaxTiltLimit - MinTiltLimit); }
                else { return _tiltPercentActual; }
            }
            private set { SetProperty(ref _tiltPercentActual, value); }
        }

        private ushort _panSpeed;
        /// <summary>
        /// Gets/Sets the Pan max change of output position the Pololu servo will set per 'tick'.
        /// </summary>
        /// <remarks>
        /// Value of '0' represents unlimited / instant.
        /// </remarks>
        public ushort PanSpeed
        {
            get { return _panSpeed; }
            set
            {
                SetProperty(ref _panSpeed, value);
                SetPanSpeed(value);
            }
        }

        private ushort _panAccel;
        /// <summary>
        /// Gets/Sets the Pan max change of speed the Pololu servo will set per 'tick'.
        /// </summary>
        /// <remarks>
        /// Value of '0' represents unlimited / instant.
        /// </remarks>
        public ushort PanAccel
        {
            get { return _panAccel; }
            set
            {
                SetProperty(ref _panAccel, value);
                SetPanAccel(value);
            }
        }

        private ushort _tiltSpeed;
        /// <summary>
        /// Gets/Sets the Tilt max change of output position the Pololu servo will set per 'tick'.
        /// </summary>
        /// <remarks>
        /// Value of '0' represents unlimited / instant.
        /// </remarks>
        public ushort TiltSpeed
        {
            get { return _tiltSpeed; }
            set
            {
                SetProperty(ref _tiltSpeed, value);
                SetTiltSpeed(value);
            }
        }

        private ushort _tiltAccel;
        /// <summary>
        /// Gets/Sets the Pan max change of speed the Pololu servo will set per 'tick'.
        /// </summary>
        /// <remarks>
        /// Value of '0' represents unlimited / instant.
        /// </remarks>
        public ushort TiltAccel
        {
            get { return _tiltAccel; }
            set
            {
                SetProperty(ref _tiltAccel, value);
                SetTiltAccel(value);
            }
        }

        #endregion


        #region Polling fields and methods

        // shamefully taken from: http://stackoverflow.com/questions/23340894/polling-the-right-way
        private static readonly int USB_POLL_PERIOD = 100; //ms
        private CancellationTokenSource _ctsUsbPoll = new CancellationTokenSource();
        private CancellationToken _tUsbPoll;

        /// <summary>
        /// Starts a background Task that will update public Pan/Tilt properties via USB polling
        /// </summary>
        public void StartPollingUsb()
        {
            _tUsbPoll = _ctsUsbPoll.Token;
            var listener = Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!IsActive) { throw new Exception("Cannot start polling while no device is active!"); }

                    

                    IsPolling = true;
                    while (true)
                    {
                        ServoStatus[] servos;
                        try { _connectedController.getVariables(out servos); }
                        catch { throw; }

                        // Pan Servo
                        PanPosActual = servos[PanServoChannel].position;
                        PanSpeed = servos[PanServoChannel].speed;
                        PanAccel = servos[PanServoChannel].acceleration;
                        PanPercentActual = 100 * (PanPosActual - MinPanLimit) / (MaxPanLimit - MinPanLimit);


                        // Tilt Servo
                        TiltPosActual = servos[TiltServoChannel].position;
                        TiltSpeed = servos[TiltServoChannel].speed;
                        TiltAccel = servos[TiltServoChannel].acceleration;
                        TiltPercentActual = 100 * (TiltPosActual - MinPanLimit) / (MaxPanLimit - MinPanLimit);

                        // Update movement flag
                        if (PanAccel == 0 || PanSpeed == 0)
                        {
                            Moving = MovementType.UNKNOWN;
                        }
                        else
                        {
                            bool panMoving = false;
                            bool tiltMoving = false;

                            if (Math.Abs(PanPosGoal - PanPosActual) > FUZZY_GOAL_RANGE) { panMoving = true; }
                            if (Math.Abs(TiltPosGoal - TiltPosActual) > FUZZY_GOAL_RANGE) { tiltMoving = true; }

                            if (panMoving && tiltMoving)
                            {
                                Moving = MovementType.BOTH;
                                if (EventsEnabled) { MovementStarted?.Invoke(this, null); }
                            }
                            else if (panMoving && !tiltMoving)
                            {
                                Moving = MovementType.PAN;
                                if (EventsEnabled) { MovementStarted?.Invoke(this, null); }
                            }
                            else if (!panMoving && tiltMoving)
                            {
                                Moving = MovementType.TILT;
                                if (EventsEnabled) { MovementStarted?.Invoke(this, null); }
                            }
                            else if (!panMoving && !tiltMoving)
                            {
                                if (Moving == MovementType.BOTH || 
                                    Moving == MovementType.PAN ||
                                    Moving == MovementType.TILT)
                                {
                                    // Send stop event if enabled
                                    if (EventsEnabled) { MovementEnded?.Invoke(this, null); }
                                }
                                Moving = MovementType.STOPPED;
                            }
                        }

                        // Sleep for the given time
                        if (_tUsbPoll.IsCancellationRequested) { break; }
                        Thread.Sleep(USB_POLL_PERIOD);
                    }
                }
                catch (Exception) { throw; }
                finally
                {
                    IsPolling = false;
                }
            }, _tUsbPoll, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Stops background USB polling via token request
        /// </summary>
        public void StopPollingUsb()
        {
            _ctsUsbPoll.Cancel();
        }

        #endregion


        #region Public Functions (also available by commands)

        /// <summary>
        /// Connects to a USB Servo Controller with the given serial
        /// </summary>
        public void ConnectModule(string deviceSerial = "")
        {
            // Get a list of all connected devices of this type.
            List<DeviceListItem> connectedDevices = Usc.getConnectedDevices();

            foreach (DeviceListItem dli in connectedDevices)
            {
                if (deviceSerial.Length > 0 && dli.serialNumber != deviceSerial) { continue; }

                _connectedController = new Usc(dli); // Connect to the device.
                IsActive = true;
                ActiveDeviceSerial = _connectedController.getSerialNumber();

                return; // End loop once we found the device
            }
            throw new Exception("Could not find device.  Make sure it is plugged in to USB " +
                "and check your Device Manager (Windows) or run lsusb (Linux).");
        }
        

        /// <summary>
        /// Connects to a USB Servo Controller with the given object
        /// </summary>
        public void ConnectModule(DeviceListItem device)
        {
            try
            {
                _connectedController = new Usc(device); // Connect to the device.
                IsActive = true;
                ActiveDeviceSerial = _connectedController.getSerialNumber();
                return;
            }
            catch (Exception)
            { 
                throw;
            }
        }


        /// <summary>
        /// Disconnects with the currently connected USB Servo controller
        /// </summary>
        public void Disconnect()
        {
            // Stop polling
            //StopPollingUsb();

            // we should attempt to disconnect even if the IsConnect flag is raised,
            // just because we may not always be in sync
            if (_connectedController == null)
            {
                Debug.WriteLine("No connected module found to disconnect from; resetting anyway...");
                IsActive = false;
                ActiveDeviceSerial = "";
                return;
            }

            try
            {
                _connectedController.Dispose();  // Disconnect
            }
            catch { throw; }
        }


        /// <summary>
        /// Called by systems to Dispose of USB connections. You should use 'Disconnect()' instead!
        /// </summary>
        /// <see cref="Disconnect"/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Called by systems to Dispose of USB connections. You should use 'Disconnect()' instead!
        /// </summary>
        /// <see cref="Disconnect"/>
        public void Dispose(bool option)
        {
            // false = only native objects
            // true = managed and native
            // doesn't matter for us practically right now, but we should have the override anyway
            try
            {
                Debug.Write("Disposing Pololu device: " + ActiveDeviceSerial);
                _connectedController.Dispose();
                _ctsUsbPoll.Dispose();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Debug.WriteLine("Failed to dispose Pololu USB module cleanly!");
                //throw;
            }
            finally
            {
                _connectedController = null;
                IsActive = false;
                ActiveDeviceSerial = "";
                Debug.WriteLine("Disconnected from: " + ActiveDeviceSerial);
            }
        }

        #endregion


        #region Module Communication Procedures

        private void SetPanPosition(ushort value)
        {
            if (value > MaxPanLimit || value < MinPanLimit) { throw new Exception("Value exceeds range limits: " + MethodBase.GetCurrentMethod().Name + " cannot be " + value); }
            else
            {
                try
                {
                    _connectedController.setTarget(PanServoChannel, value);
                }
                catch { throw; }
            }
        }

        private void SetTiltPosition(ushort value)
        {
            if (value > MaxTiltLimit || value < MinTiltLimit) { throw new Exception("Value exceeds range limits: " + MethodBase.GetCurrentMethod().Name + " cannot be " + value); }
            else
            {
                try
                {
                    _connectedController.setTarget(TiltServoChannel, value);
                }
                catch { throw; }
            }
        }

        private void SetPanSpeed(ushort value)
        {
            try
            {
                _connectedController.setSpeed(PanServoChannel, value);
            }
            catch { throw; }
        }

        private void SetPanAccel(ushort value)
        {
            try
            {
                _connectedController.setAcceleration(PanServoChannel, value);
            }
            catch { throw; }
        }

        private void SetTiltSpeed(ushort value)
        {
            try
            {
                _connectedController.setSpeed(TiltServoChannel, value);
            }
            catch { throw; }
        }

        private void SetTiltAccel(ushort value)
        {
            try
            {
                _connectedController.setAcceleration(TiltServoChannel, value);
            }
            catch { throw; }
        }

        private ushort GetPanPos()
        {
            try
            {
                _connectedController.getVariables(out ServoStatus[] servos);
                return servos[PanServoChannel].position;
            }
            catch { throw; }
        }

        private ushort GetTiltPos()
        {
            try
            {
                _connectedController.getVariables(out ServoStatus[] servos);
                return servos[TiltServoChannel].position;
            }
            catch { throw; }
        }

        private ushort GetPanSpeed()
        {
            try
            {
                _connectedController.getVariables(out ServoStatus[] servos);
                return servos[PanServoChannel].speed;
            }
            catch (Exception) { throw; }
        }

        private ushort GetTiltSpeed()
        {
            try
            {
                _connectedController.getVariables(out ServoStatus[] servos);
                return servos[TiltServoChannel].speed;
            }
            catch (Exception) { throw; }
        }

        private ushort GetPanAccel()
        {
            try
            {
                _connectedController.getVariables(out ServoStatus[] servos);
                return servos[PanServoChannel].acceleration;
            }
            catch (Exception) { throw; }
        }

        private ushort GetTiltAccel()
        {
            try
            {
                _connectedController.getVariables(out ServoStatus[] servos);
                return servos[TiltServoChannel].acceleration;
            }
            catch (Exception) { throw; }
        } 

        #endregion
    }

}
