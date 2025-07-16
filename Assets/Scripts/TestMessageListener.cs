using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMessageListener : MonoBehaviour
{
    // Invoked when a line of data is received from the serial device.
    void OnMessageArrived(string msg)
    {
       Debug.Log("Message received: " + msg);
    }

    // Invoked when a connect/disconnect event occurs. The parameter 'success'
    // will be 'true' upon connection, and 'false' upon disconnection or
    // failure to connect.
    void OnConnectionEvent(bool success)
    {
        Debug.Log("Connection event: " + (success ? "Connected" : "Disconnected"));
    }
}
