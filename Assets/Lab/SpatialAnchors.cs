using System;
using System.Collections;
using System.Collections.Generic;
using OpenCover.Framework.Model;
using UnityEngine;
using static OVRInput;
using TMPro;

public class SpatialAnchors : MonoBehaviour
{
    //Specify controller to create Spatial Anchors
    [SerializeField] private Controller controller;
    private int count = 0;
    // Spatial Anchor Prefab
    public GameObject anchorPrefab;
    private Canvas canvas;
    private TextMeshProUGUI idText;
    private TextMeshProUGUI positionText;

    private const float Speed = 0.4f;
    
    // Calibration Interop
    public TCP Server;
    private bool HasCalibrated = false;
    
    private Transform UnityMarkerOwner;
    private Transform RealsenseMarkerOwner;

    public void Start()
    {
        UnityMarkerOwner = this.transform.Find("UnityCreated");
        if (UnityMarkerOwner == null)
            Debug.LogError("No UnityCreated found");
        
        RealsenseMarkerOwner = this.transform.Find("RealsenseCreated");
        if (RealsenseMarkerOwner == null)
            Debug.LogError("No RealsenseCreated found");
    }

    // Update is called once per frame
    void Update()
    {
        // Create Anchor when user press the index trigger on specified controller
        if(OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
        {
            CreateSpatialAnchorForController();
        }

        // Manage self position for adjustments
        Vector2 sideAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, Controller.RTouch);
        Vector2 topAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, Controller.LTouch);
        Vector3 translation = new Vector3(sideAxis.x, topAxis.y, sideAxis.y) * Speed * Time.deltaTime;

        RealsenseMarkerOwner.Rotate(Vector3.up, topAxis.x * Mathf.Rad2Deg * Speed * Time.deltaTime);
        RealsenseMarkerOwner.Translate(translation, Space.World);
        
        if (HasCalibrated == false && OVRInput.Get(OVRInput.Button.One) && UnityMarkerOwner.childCount >= 4)
        {
            Server.ShouldSendCalibrate = true;
            Debug.LogWarning("CONTROLLER: Sent calibrate request");
            HasCalibrated = true;
        }

        if (HasCalibrated == true && OVRInput.Get(OVRInput.Button.Two))
        {
            string outJSONPosition = JsonUtility.ToJson(RealsenseMarkerOwner.position);
            System.IO.File.WriteAllText("CalibrationPostShift.json", outJSONPosition);
        }
    }
    
    public void CreateSpatialAnchorForController()
    {
        // Create anchor at Controller Position and Rotation
        GameObject anchor = Instantiate(anchorPrefab, OVRInput.GetLocalControllerPosition(controller), OVRInput.GetLocalControllerRotation(controller));
        
        canvas = anchor.GetComponentInChildren<Canvas>();
        
        // Show anchor id
        idText = canvas.gameObject.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        idText.text = "#: " + count.ToString();

        // Show anchor position
        positionText = canvas.gameObject.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        positionText.text = anchor.transform.GetChild(0).GetChild(0).position.ToString();

        // Make the anchor become a Meta Quest Spatial Anchor
        anchor.AddComponent<OVRSpatialAnchor>();

        // Add it to manager
        anchor.transform.SetParent(UnityMarkerOwner);

        // Increase Id by 1
        count += 1;
    }
    
    public GameObject CreateSpatialAnchorForRealsense(Vector3 position, int id)
    {
        // Create anchor at Controller Position and Rotation
        GameObject anchor = Instantiate(anchorPrefab, Vector3.zero, Quaternion.identity);
        anchor.transform.localPosition = position;
        anchor.name = id.ToString();
        
        canvas = anchor.GetComponentInChildren<Canvas>();
        
        // Show anchor id
        idText = canvas.gameObject.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        idText.text = $"ID: {id}";

        // Show anchor position
        positionText = canvas.gameObject.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        positionText.text = anchor.transform.GetChild(0).GetChild(0).position.ToString();
        
        anchor.transform.SetParent(RealsenseMarkerOwner);

        return anchor;
    }
}
