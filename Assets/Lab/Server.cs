/*
Reference
Implementing a Basic TCP Server in Unity: A Step-by-Step Guide
By RabeeQiblawi Nov 20, 2023
https://medium.com/@rabeeqiblawi/implementing-a-basic-tcp-server-in-unity-a-step-by-step-guide-449d8504d1c5
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Lab;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class TCP : MonoBehaviour
{
    const string hostIP = "127.0.0.1"; // Select your IP
    const int port = 80; // Select your port
    TcpListener server = null;
    TcpClient client = null;
    NetworkStream stream = null;
    Thread thread;

    // Define your own message
    [DoNotSerialize, HideInInspector]
    public bool ShouldSendCalibrate = false;

    public SpatialAnchors AnchorManager;
    GameDaemon GameDaemon;
    
    public Transform Minecart;
    public int Minecart_ARUCO_ID = 50;
    public int Button_ARUCO_ID = 7;
    [FormerlySerializedAs("IsButton")] public bool SignalMarkerIsVisible = true;
    
    private Transform UnityMarker;
    private Transform UnityMarkerOwner;
    private Transform RealsenseMarkerOwner;
    
    [Serializable]
    public class Message
    {
        public int[] OutgoingIds;
        public Vector3[] OutgoingPositions;

        public int[] IncomingIds;
        public Vector3[] IncomingPositions;
        
        public float LHand_x;
        public float LHand_y;
        public float LHand_z;
        public float RHand_x;
        public float RHand_y;
        public float RHand_z;
        public float Head_x;
        public float Head_y;
        public float Head_z;
    }
    
    private static Vector3 QUEUE_REMOVAL_POSITION = new Vector3(-999.0f, -999.0f, -999.0f);

    private float timer = 0;
    private static object Lock = new object();  // lock to prevent conflict in main thread and server thread
    private List<Message> MessageQueue = new List<Message>();

    private void Start()
    {
        UnityMarkerOwner = AnchorManager.transform.Find("UnityCreated");
        if (UnityMarkerOwner == null)
            Debug.LogError("No UnityCreated found");
        
        RealsenseMarkerOwner = AnchorManager.transform.Find("RealsenseCreated");
        if (RealsenseMarkerOwner == null)
            Debug.LogError("No RealsenseCreated found");
        
        thread = new Thread(new ThreadStart(SetupServer));
        thread.Start();
    }

    private bool ServerConnected = false;

    //private static int[] TempLut = new int[] { 9, 8, 5, 1 };
    private static int[] TempLut = new int[] { 1, 2, 3, 4 };

    private void Update()
    {
        if (ServerConnected == false)
            return;
        
        if (ShouldSendCalibrate)
        {
            Message msg = new Message();

            int childCount = UnityMarkerOwner.childCount;
            
            msg.OutgoingIds = new int[childCount];
            msg.OutgoingPositions = new Vector3[childCount];
            for (int i = 0; i < childCount; i++)
            {
                Transform anchor = UnityMarkerOwner.GetChild(i);
                //anchor = anchor.GetChild(0);
                
                //msg.OutgoingIds[i] = i + 1;
                msg.OutgoingIds[i] = TempLut[i];
                msg.OutgoingPositions[i] = anchor.position;
                
                Debug.LogWarning($"Position: {anchor.position}");
            }
            
            Debug.Log($"Sent positions for {childCount} anchors!");

            ShouldSendCalibrate = false;
            
            SendMessageToClient(msg);
        }
        
        lock(Lock)
        {
            foreach (Message message in MessageQueue)
            {
                int[] incomingIds = message.IncomingIds;
                Vector3[] incomingPositions = message.IncomingPositions;

                for (int i = 0; i < incomingIds.Length; i++)
                {
                    int id = incomingIds[i];
                    Vector3 incomingPosition = incomingPositions[i];

                    if (id == Minecart_ARUCO_ID)
                    {
                        if (incomingPosition != QUEUE_REMOVAL_POSITION)
                            Minecart.localPosition = new Vector3(incomingPosition.x, 0, incomingPosition.z);
                    }
                    else if (id == Button_ARUCO_ID)
                    {
                        if (SignalMarkerIsVisible && incomingPosition == QUEUE_REMOVAL_POSITION)
                            GameDaemon.OnMarkerHidden();
                        else if (!SignalMarkerIsVisible && incomingPosition != QUEUE_REMOVAL_POSITION)
                            GameDaemon.OnMarkerShown();
                        
                        SignalMarkerIsVisible = incomingPosition != QUEUE_REMOVAL_POSITION;
                    }
                        
                    Transform markerObject = RealsenseMarkerOwner.Find(id.ToString());
                    
                    if (incomingPosition == QUEUE_REMOVAL_POSITION)
                    {
                        if (markerObject != null)
                            Destroy(markerObject.gameObject);
                    }
                    else
                    {
                        if (markerObject == null)
                        {
                            GameObject markerGameObject = AnchorManager.CreateSpatialAnchorForRealsense(incomingPosition, id);
                        }
                        else
                        {
                            markerObject.localPosition = incomingPosition;
                        }
                    }
                }
            }
            MessageQueue.Clear();
        }
    }

    private void SetupServer()
    {
        try
        {
            IPAddress localAddr = IPAddress.Parse(hostIP);
            server = new TcpListener(localAddr, port);
            server.Start();

            byte[] buffer = new byte[1024];
            string data = null;

            while (true)
            {
                Debug.Log("Waiting for connection...");
                client = server.AcceptTcpClient();
                Debug.Log("Connected!");

                ServerConnected = true;

                data = null;
                stream = client.GetStream();

                // Receive message from client    
                int i;
                while ((i = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    data = Encoding.UTF8.GetString(buffer, 0, i);
                    Message message = Decode(data);
                    // Add received message to queue
                    lock(Lock)
                    {
                        MessageQueue.Add(message);
                    }
                }
                client.Close();
            }
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException: " + e);
        }
        finally
        {
            server.Stop();
        }
    }

    private void OnApplicationQuit()
    {
        stream.Close();
        client.Close();
        server.Stop();
        thread.Abort();
    }

    public void SendMessageToClient(Message message)
    {
        byte[] msg = Encoding.UTF8.GetBytes(Encode(message));
        stream.Write(msg, 0, msg.Length);
        Debug.Log("Sent: " + message);
    }

    // Encode message from struct to Json String
    public string Encode(Message message)
    {
        return JsonUtility.ToJson(message, true);
    }

    // Decode messaage from Json String to struct
    public Message Decode(string json_string)
    {
        Message msg = JsonUtility.FromJson<Message>(json_string);
        return msg;
    }
}
