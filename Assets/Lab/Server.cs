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

public class Server : MonoBehaviour
{
    // Setup Linkage
    public Transform UnityRoot;
    public Transform RealsenseRoot;
    public SpatialAnchors AnchorManager;
    public GameDaemon GameDaemon;
    
    // Game Object Linkage
    public Transform Minecart;
    public Transform NPC_LHand;
    public Transform NPC_RHand;
    public Transform NPC_Head;
    
    // SIGNAL
    private bool ServerConnected = false;
    [DoNotSerialize, HideInInspector]
    public bool ShouldSendCalibrate = false;
    private bool SignalMarkerIsVisible = true;
    
    // Constants
    private int MINECART_ARUCO_ID = 50;
    private int BUTTON_ARUCO_ID = 7;
    private static int[] MARKER_INDEX_TABLE = { 3, 5, 10, 50 };
    private static Vector3 QUEUE_REMOVAL_POSITION = new (-999.0f, -999.0f, -999.0f);
    
    // Cached objects
    private Transform UnityMarker;
    private Transform UnityMarkerOwner;
    private Transform RealsenseMarkerOwner;
    
    // Message
    [Serializable]
    public class Message
    {
        public int[] OutgoingIds;
        public Vector3[] OutgoingPositions;

        public int[] IncomingIds;
        public Vector3[] IncomingPositions;

        public Vector3 LHand;
        public Vector3 RHand;
        public Vector3 Head;

        public Vector3 CalibratedOrigin;
        public Vector3 CalibratedForward;
    }

    private void Start()
    {
        UnityMarkerOwner = AnchorManager.transform.Find("UnityCreated");
        if (UnityMarkerOwner == null)
            Debug.LogError("SETUP: No UnityCreated found");
        
        RealsenseMarkerOwner = AnchorManager.transform.Find("RealsenseCreated");
        if (RealsenseMarkerOwner == null)
            Debug.LogError("SETUP: No RealsenseCreated found");
        
        thread = new Thread(new ThreadStart(SetupServer));
        thread.Start();
    }
    
    public void SetNPCPose(Message message)
    {
        Vector3 lHand = message.LHand;
        Vector3 rHand = message.RHand;
        Vector3 head = message.Head;
        
        NPC_LHand.localPosition = lHand;
        NPC_RHand.localPosition = rHand;
        NPC_Head.localPosition = head;
    }
    
    private void Update()
    {
        if (ServerConnected == false)
            return;
        
        // ------------
        // Calibration
        // ------------
        if (ShouldSendCalibrate)
        {
            int markerCount = UnityMarkerOwner.childCount;
            
            Message msg = new Message();
            msg.OutgoingIds = new int[markerCount];
            msg.OutgoingPositions = new Vector3[markerCount];
            for (int i = 0; i < markerCount; i++)
            {
                Transform anchor = UnityMarkerOwner.GetChild(i);
                
                int id = MARKER_INDEX_TABLE[i];
                Vector3 position = anchor.position;
                msg.OutgoingIds[i] = id;
                msg.OutgoingPositions[i] = position;
                
                Debug.Log($"CALIBRATION: Sent {anchor.position} for {id}");
            }
            
            Debug.Log($"CALIBRATION: Sent positions for {markerCount} markers!");

            ShouldSendCalibrate = false;
            
            SendMessageToClient(msg);
        }
          
        // --------------
        // Normal Runtime
        // --------------
        lock(Lock)
        {
            foreach (Message message in MessageQueue)
            {
                Vector3 calibratedOrigin = message.CalibratedOrigin;
                calibratedOrigin.y = 0;
                UnityRoot.position = calibratedOrigin;
                
                Vector3 calibratedForward = message.CalibratedForward - message.CalibratedOrigin;
                calibratedForward.y = 0;
                calibratedForward.Normalize();
                UnityRoot.forward = calibratedForward;
                
                int[] incomingIds = message.IncomingIds;
                Vector3[] incomingPositions = message.IncomingPositions;

                for (int i = 0; i < incomingIds.Length; i++)
                {
                    int id = incomingIds[i];
                    Vector3 incomingPosition = incomingPositions[i];

                    if (id == MINECART_ARUCO_ID)
                    {
                        if (incomingPosition != QUEUE_REMOVAL_POSITION)
                            Minecart.localPosition = new Vector3(incomingPosition.x, 0, incomingPosition.z);
                    }
                    else if (id == BUTTON_ARUCO_ID)
                    {
                        if (SignalMarkerIsVisible && incomingPosition == QUEUE_REMOVAL_POSITION)
                            GameDaemon.OnMarkerHidden();
                        else if (!SignalMarkerIsVisible && incomingPosition != QUEUE_REMOVAL_POSITION)
                            GameDaemon.OnMarkerShown();
                        
                        SignalMarkerIsVisible = incomingPosition != QUEUE_REMOVAL_POSITION;
                    }
                        
                    // Remove and remove ALL markers for bookkeeping
                    Transform markerObject = RealsenseMarkerOwner.Find(id.ToString());
                    if (incomingPosition == QUEUE_REMOVAL_POSITION)
                    {
                        if (markerObject != null)
                            Destroy(markerObject.gameObject);
                    }
                    else
                    {
                        if (markerObject == null)
                            AnchorManager.CreateSpatialAnchorForRealsense(incomingPosition, id);
                        else
                            markerObject.localPosition = incomingPosition;
                    }
                }
                
                SetNPCPose(message);
            }
            MessageQueue.Clear();
        }
    }
    
    // ================================
    // SERVER UTILS
    // ================================
    private const string hostIP = "127.0.0.1"; // Select your IP
    private const int port = 80; // Select your port
    private TcpListener server = null;
    private TcpClient client = null;
    private NetworkStream stream = null;
    private Thread thread;
    
    private static object Lock = new object();  // lock to prevent conflict in main thread and server thread
    private List<Message> MessageQueue = new List<Message>();
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
    public string Encode(Message message) => JsonUtility.ToJson(message, true);
    public Message Decode(string json_string) => JsonUtility.FromJson<Message>(json_string);
}
