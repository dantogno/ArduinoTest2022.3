using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMessageListener : MonoBehaviour
{
    // Invoked when a line of data is received from the serial device.
    void OnMessageArrived(string msg)
    {
        Debug.Log("Arduino says: " + msg);
        
        // Add your game logic here
        // For example:
        // if (msg == "button_pressed") { /* do something */ }
    }

    // Invoked when a connect/disconnect event occurs.
    void OnConnectionEvent(bool success)
    {
        if (success)
            Debug.Log("Arduino connected!");
        else
            Debug.Log("Arduino disconnected!");
    }
}
