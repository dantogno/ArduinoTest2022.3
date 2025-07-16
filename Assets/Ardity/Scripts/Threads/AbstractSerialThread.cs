/**
 * Ardity (Serial Communication for Arduino + Unity)
 * Author: Daniel Wilches <dwilches@gmail.com>
 *
 * This work is released under the Creative Commons Attributions license.
 * https://creativecommons.org/licenses/by/2.0/
 */

using UnityEngine;

using System;
using System.IO;
using System.IO.Ports;
using System.Collections;
using System.Threading;

/**
 * This class contains methods that must be run from inside a thread and others
 * that must be invoked from Unity. Both types of methods are clearly marked in
 * the code, although you, the final user of this library, don't need to even
 * open this file unless you are introducing incompatibilities for upcoming
 * versions.
 */
public abstract class AbstractSerialThread
{
    // Parameters passed from SerialController, used for connecting to the
    // serial device as explained in the SerialController documentation.
    private string portName;
    private int baudRate;
    private int delayBeforeReconnecting;
    private int maxUnreadMessages;

    // Object from the .Net framework used to communicate with serial devices.
    private SerialPort serialPort;

    // Amount of milliseconds alloted to a single read or connect. An
    // exception is thrown when such operations take more than this time
    // to complete.
    private const int readTimeout = 100;

    // Amount of milliseconds alloted to a single write. An exception is thrown
    // when such operations take more than this time to complete.
    private const int writeTimeout = 100;

    // Internal synchronized queues used to send and receive messages from the
    // serial device. They serve as the point of communication between the
    // Unity thread and the SerialComm thread.
    private Queue inputQueue, outputQueue;

    // Indicates when this thread should stop executing. When SerialController
    // invokes 'RequestStop()' this variable is set.
    private bool stopRequested = false;

    private bool enqueueStatusMessages = false;

    // Debug counters
    private int messageCount = 0;
    private int timeoutCount = 0;
    private int runOnceCount = 0;
    private DateTime lastLogTime = DateTime.Now;


    /**************************************************************************
     * Methods intended to be invoked from the Unity thread.
     *************************************************************************/

    // ------------------------------------------------------------------------
    // Constructs the thread object. This object is not a thread actually, but
    // its method 'RunForever' can later be used to create a real Thread.
    // ------------------------------------------------------------------------
    public AbstractSerialThread(string portName,
                                int baudRate,
                                int delayBeforeReconnecting,
                                int maxUnreadMessages,
                                bool enqueueStatusMessages)
    {
        this.portName = portName;
        this.baudRate = baudRate;
        this.delayBeforeReconnecting = delayBeforeReconnecting;
        this.maxUnreadMessages = maxUnreadMessages;
        this.enqueueStatusMessages = enqueueStatusMessages;

        inputQueue = Queue.Synchronized(new Queue());
        outputQueue = Queue.Synchronized(new Queue());
        
        Debug.Log("AbstractSerialThread: Constructor called with port=" + portName + ", baud=" + baudRate + ", enqueueStatus=" + enqueueStatusMessages);
    }

    // ------------------------------------------------------------------------
    // Invoked to indicate to this thread object that it should stop.
    // ------------------------------------------------------------------------
    public void RequestStop()
    {
        lock (this)
        {
            stopRequested = true;
        }
        Debug.Log("AbstractSerialThread: Stop requested");
    }

    // ------------------------------------------------------------------------
    // Polls the internal message queue returning the next available message
    // in a generic form. This can be invoked by subclasses to change the
    // type of the returned object.
    // It returns null if no message has arrived since the latest invocation.
    // ------------------------------------------------------------------------
    public object ReadMessage()
    {
        if (inputQueue.Count == 0)
            return null;

        object message = inputQueue.Dequeue();
        Debug.Log("AbstractSerialThread: ReadMessage returning: '" + message + "' (Queue count: " + inputQueue.Count + ")");
        return message;
    }

    // ------------------------------------------------------------------------
    // Schedules a message to be sent. It writes the message to the
    // output queue, later the method 'RunOnce' reads this queue and sends
    // the message to the serial device.
    // ------------------------------------------------------------------------
    public void SendMessage(object message)
    {
        outputQueue.Enqueue(message);
        Debug.Log("AbstractSerialThread: SendMessage queued: '" + message + "'");
    }


    /**************************************************************************
     * Methods intended to be invoked from the SerialComm thread (the one
     * created by the SerialController).
     *************************************************************************/

    // ------------------------------------------------------------------------
    // Enters an almost infinite loop of attempting connection to the serial
    // device, reading messages and sending messages. This loop can be stopped
    // by invoking 'RequestStop'.
    // ------------------------------------------------------------------------
    public void RunForever()
    {
        Debug.Log("AbstractSerialThread: RunForever started for port " + portName);
        
        // This 'try' is for having a log message in case of an unexpected
        // exception.
        try
        {
            while (!IsStopRequested())
            {
                try
                {
                    AttemptConnection();

                    Debug.Log("AbstractSerialThread: Entering RunOnce loop");
                    
                    // Enter the semi-infinite loop of reading/writing to the
                    // device.
                    while (!IsStopRequested())
                    {
                        RunOnce();
                        
                        // Log every 100 iterations to see if loop is running
                        runOnceCount++;
                        if (runOnceCount % 100 == 0)
                        {
                            Debug.Log("AbstractSerialThread: RunOnce called " + runOnceCount + " times. Messages: " + messageCount + ", Timeouts: " + timeoutCount);
                        }
                    }
                    
                    Debug.Log("AbstractSerialThread: Exited RunOnce loop due to stop request");
                }
                catch (Exception ioe)
                {
                    // A disconnection happened, or there was a problem
                    // reading/writing to the device. Log the detailed message
                    // to the console and notify the listener.
                    Debug.LogWarning("AbstractSerialThread Exception: " + ioe.Message + " StackTrace: " + ioe.StackTrace);
                    if (enqueueStatusMessages)
                        inputQueue.Enqueue(SerialController.SERIAL_DEVICE_DISCONNECTED);

                    // As I don't know in which stage the SerialPort threw the
                    // exception I call this method that is very safe in
                    // disregard of the port's status
                    CloseDevice();

                    // Don't attempt to reconnect just yet, wait some
                    // user-defined time. It is OK to sleep here as this is not
                    // Unity's thread, this doesn't affect frame-rate
                    // throughput.
                    Debug.Log("AbstractSerialThread: Waiting " + delayBeforeReconnecting + "ms before reconnecting");
                    Thread.Sleep(delayBeforeReconnecting);
                }
            }

            Debug.Log("AbstractSerialThread: Exited main loop due to stop request");

            // Before closing the COM port, give the opportunity for all messages
            // from the output queue to reach the other endpoint.
            while (outputQueue.Count != 0)
            {
                SendToWire(outputQueue.Dequeue(), serialPort);
            }

            // Attempt to do a final cleanup. This method doesn't fail even if
            // the port is in an invalid status.
            CloseDevice();
        }
        catch (Exception e)
        {
            Debug.LogError("AbstractSerialThread: Unknown exception: " + e.Message + " " + e.StackTrace);
        }
        
        Debug.Log("AbstractSerialThread: RunForever ended");
    }

    // ------------------------------------------------------------------------
    // Try to connect to the serial device. May throw IO exceptions.
    // ------------------------------------------------------------------------
    private void AttemptConnection()
    {
        Debug.Log("AbstractSerialThread: Attempting connection to " + portName + " at " + baudRate + " baud");
        
        serialPort = new SerialPort(portName, baudRate);
        serialPort.ReadTimeout = readTimeout;
        serialPort.WriteTimeout = writeTimeout;
        
        // Try enabling DTR/RTS instead of disabling them
        serialPort.DtrEnable = true;
        serialPort.RtsEnable = true;
        
        // Set additional serial port parameters
        serialPort.DataBits = 8;
        serialPort.Parity = Parity.None;
        serialPort.StopBits = StopBits.One;
        serialPort.Handshake = Handshake.None;
        
        serialPort.Open();

        Debug.Log("AbstractSerialThread: Connection successful. Port is open: " + serialPort.IsOpen);
        
        // Add a delay after opening to let Arduino settle
        Thread.Sleep(2000);
        Debug.Log("AbstractSerialThread: Waited 2 seconds for Arduino to settle");
        
        // Clear any existing data in the buffer
        serialPort.DiscardInBuffer();
        serialPort.DiscardOutBuffer();
        Debug.Log("AbstractSerialThread: Cleared serial buffers");

        if (enqueueStatusMessages)
        {
            inputQueue.Enqueue(SerialController.SERIAL_DEVICE_CONNECTED);
            Debug.Log("AbstractSerialThread: CONNECTED message enqueued");
        }
    }

    // ------------------------------------------------------------------------
    // Release any resource used, and don't fail in the attempt.
    // ------------------------------------------------------------------------
    private void CloseDevice()
    {
        if (serialPort == null)
            return;

        try
        {
            Debug.Log("AbstractSerialThread: Closing serial port");
            serialPort.Close();
        }
        catch (IOException ioe)
        {
            Debug.LogWarning("AbstractSerialThread: IOException while closing port: " + ioe.Message);
        }

        serialPort = null;
    }

    // ------------------------------------------------------------------------
    // Just checks if 'RequestStop()' has already been called in this object.
    // ------------------------------------------------------------------------
    private bool IsStopRequested()
    {
        lock (this)
        {
            return stopRequested;
        }
    }

    // ------------------------------------------------------------------------
    // A single iteration of the semi-infinite loop. Attempt to read/write to
    // the serial device. If there are more lines in the queue than we may have
    // at a given time, then the newly read lines will be discarded. This is a
    // protection mechanism when the port is faster than the Unity progeram.
    // If not, we may run out of memory if the queue really fills.
    // ------------------------------------------------------------------------
    private void RunOnce()
    {
        try
        {
            // Send a message.
            if (outputQueue.Count != 0)
            {
                object outMessage = outputQueue.Dequeue();
                Debug.Log("AbstractSerialThread: Sending message: '" + outMessage + "'");
                SendToWire(outMessage, serialPort);
            }

            // Read a message.
            // If a line was read, and we have not filled our queue, enqueue
            // this line so it eventually reaches the Message Listener.
            // Otherwise, discard the line.
            object inputMessage = ReadFromWire(serialPort);
            if (inputMessage != null)
            {
                messageCount++;
                Debug.Log("AbstractSerialThread: Message received (#" + messageCount + "): '" + inputMessage + "'");
                
                if (inputQueue.Count < maxUnreadMessages)
                {
                    inputQueue.Enqueue(inputMessage);
                    Debug.Log("AbstractSerialThread: Message enqueued. Queue count: " + inputQueue.Count);
                }
                else
                {
                    Debug.LogWarning("AbstractSerialThread: Queue is full. Dropping message: " + inputMessage);
                }
            }
        }
        catch (TimeoutException)
        {
            // This is normal, not everytime we have a report from the serial device
            timeoutCount++;
            
            // Log timeout stats every 50 timeouts (about 5 seconds)
            if (timeoutCount % 50 == 0)
            {
                DateTime now = DateTime.Now;
                double elapsed = (now - lastLogTime).TotalSeconds;
                Debug.Log("AbstractSerialThread: " + timeoutCount + " timeouts so far. Messages received: " + messageCount + ". Time elapsed: " + elapsed.ToString("F1") + "s");
                lastLogTime = now;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("AbstractSerialThread: Unexpected exception in RunOnce: " + ex.Message + " StackTrace: " + ex.StackTrace);
            throw; // Re-throw to trigger reconnection
        }
    }

    // ------------------------------------------------------------------------
    // Sends a message through the serialPort.
    // ------------------------------------------------------------------------
    protected abstract void SendToWire(object message, SerialPort serialPort);

    // ------------------------------------------------------------------------
    // Reads and returns a message from the serial port.
    // ------------------------------------------------------------------------
    protected abstract object ReadFromWire(SerialPort serialPort);
}
