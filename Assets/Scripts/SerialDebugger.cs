using UnityEngine;

public class SerialDebugger : MonoBehaviour
{
    private SerialController serialController;
    
    void Start()
    {
        // Find the SerialController
        serialController = GameObject.Find("SerialController").GetComponent<SerialController>();
        
        if (serialController == null)
        {
            Debug.LogError("SerialController not found!");
            return;
        }
        
        Debug.Log("=== SERIAL DEBUGGER STARTED ===");
        Debug.Log("Port Name: " + serialController.portName);
        Debug.Log("Baud Rate: " + serialController.baudRate);
        Debug.Log("Max Unread Messages: " + serialController.maxUnreadMessages);
        Debug.Log("Message Listener: " + (serialController.messageListener != null ? serialController.messageListener.name : "NULL"));
        
        // Temporarily disable the message listener to use polling mode
        serialController.messageListener = null;
        Debug.Log("Switched to polling mode for debugging");
    }
    
    void Update()
    {
        if (serialController == null) return;
        
        // Poll for messages manually
        string message = serialController.ReadSerialMessage();
        
        if (message != null)
        {
            Debug.Log("=== RAW MESSAGE RECEIVED ===");
            Debug.Log("Message: '" + message + "'");
            Debug.Log("Length: " + message.Length);
            Debug.Log("Is Connected Constant: " + ReferenceEquals(message, SerialController.SERIAL_DEVICE_CONNECTED));
            Debug.Log("Is Disconnected Constant: " + ReferenceEquals(message, SerialController.SERIAL_DEVICE_DISCONNECTED));
            
            // Print each character's ASCII value
            string asciiDebug = "ASCII values: ";
            for (int i = 0; i < message.Length; i++)
            {
                asciiDebug += "[" + i + "]=" + (int)message[i] + " ";
            }
            Debug.Log(asciiDebug);
            Debug.Log("================================");
        }
    }
}