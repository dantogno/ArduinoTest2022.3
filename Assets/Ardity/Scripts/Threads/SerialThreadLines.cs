/**
 * Ardity (Serial Communication for Arduino + Unity)
 * Author: Daniel Wilches <dwilches@gmail.com>
 *
 * This work is released under the Creative Commons Attributions license.
 * https://creativecommons.org/licenses/by/2.0/
 */

using UnityEngine;

using System.IO.Ports;

/**
 * This class contains methods that must be run from inside a thread and others
 * that must be invoked from Unity. Both types of methods are clearly marked in
 * the code, although you, the final user of this library, don't need to even
 * open this file unless you are introducing incompatibilities for upcoming
 * versions.
 * 
 * For method comments, refer to the base class.
 */
public class SerialThreadLines : AbstractSerialThread
{
    private int readAttempts = 0;
    
    public SerialThreadLines(string portName,
                             int baudRate,
                             int delayBeforeReconnecting,
                             int maxUnreadMessages)
        : base(portName, baudRate, delayBeforeReconnecting, maxUnreadMessages, true)
    {
        Debug.Log("SerialThreadLines: Constructor called");
    }

    protected override void SendToWire(object message, SerialPort serialPort)
    {
        string stringMessage = (string)message;
        Debug.Log("SerialThreadLines: SendToWire called with: '" + stringMessage + "'");
        serialPort.WriteLine(stringMessage);
    }

    protected override object ReadFromWire(SerialPort serialPort)
    {
        readAttempts++;
        
        // Log every 50 read attempts to see what's happening
        if (readAttempts % 50 == 0)
        {
            Debug.Log("SerialThreadLines: ReadFromWire attempt #" + readAttempts + 
                     ". Port open: " + serialPort.IsOpen + 
                     ", BytesToRead: " + serialPort.BytesToRead +
                     ", ReadTimeout: " + serialPort.ReadTimeout);
        }
        
        try
        {
            // Check if there are any bytes available to read
            if (serialPort.BytesToRead > 0)
            {
                Debug.Log("SerialThreadLines: " + serialPort.BytesToRead + " bytes available to read");
            }
            
            string result = serialPort.ReadLine();
            Debug.Log("SerialThreadLines: ReadFromWire received: '" + result + "' (Length: " + result.Length + ")");
            return result;
        }
        catch (System.TimeoutException)
        {
            // Log first few timeouts to confirm they're happening
            if (readAttempts <= 10)
            {
                Debug.Log("SerialThreadLines: ReadFromWire timeout #" + readAttempts);
            }
            return null;
        }
        catch (System.InvalidOperationException ex)
        {
            Debug.LogError("SerialThreadLines: InvalidOperationException - Port may be closed: " + ex.Message);
            throw;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("SerialThreadLines: ReadFromWire unexpected exception: " + ex.GetType().Name + " - " + ex.Message);
            throw;
        }
    }
}
