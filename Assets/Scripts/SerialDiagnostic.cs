using UnityEngine;

public class SerialDiagnostic : MonoBehaviour
{
    private SerialController serialController;
    private float nextLogTime = 0f;
    
    void Start()
    {
        Debug.Log("=== SERIAL DIAGNOSTIC STARTED ===");
        
        // Find SerialController
        serialController = FindObjectOfType<SerialController>();
        if (serialController == null)
        {
            Debug.LogError("No SerialController found in scene!");
            return;
        }
        
        Debug.Log("SerialController found: " + serialController.name);
        Debug.Log("Port: " + serialController.portName);
        Debug.Log("Baud Rate: " + serialController.baudRate);
        Debug.Log("Max Unread Messages: " + serialController.maxUnreadMessages);
        Debug.Log("Message Listener: " + (serialController.messageListener ? serialController.messageListener.name : "NULL"));
        
        // Check if message listener has the correct script
        if (serialController.messageListener != null)
        {
            var listener = serialController.messageListener.GetComponent<TestMessageListener>();
            if (listener != null)
            {
                Debug.Log("TestMessageListener script found and active: " + listener.enabled);
            }
            else
            {
                Debug.LogError("TestMessageListener script NOT found on messageListener GameObject!");
            }
        }
    }
    
    void Update()
    {
        // Log diagnostic info every 5 seconds
        if (Time.time > nextLogTime)
        {
            nextLogTime = Time.time + 5f;
            
            if (serialController != null)
            {
                // Try polling manually
                string message = serialController.ReadSerialMessage();
                if (message != null)
                {
                    Debug.Log("DIAGNOSTIC: Manual polling received: '" + message + "'");
                }
                else
                {
                    Debug.Log("DIAGNOSTIC: No message available via polling at " + Time.time);
                }
            }
        }
    }
}