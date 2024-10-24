"""
SIMPLE CLIENT FOR SOCKET CLIENT
"""

import socket
import select
import json
import pyrealsense2 as rs
import numpy as np
import cv2

HOST = "127.0.0.1"  # The server's hostname or IP address
PORT = 80           # The port used by the server

# ================
# Data
# ================
HasCalibrated = False
CalibrationMatrix = np.zeros((4, 4))
MarkerCentroids = np.zeros((250, 3))
MarkerAges = np.full(250, -1)
CurrentTime = 0

LIFETIME_THRESHOLD = 3
DEBUG = False
DEBUG = True

# ================
# Networking Utils
# ================
def receive(sock):
	data = sock.recv(4*1024)
	data = data.decode('utf-8')
	msg = json.loads(data)
	print("Received: ", msg)
	return msg

def send(sock, msg):
	data = json.dumps(msg)
	sock.sendall(data.encode('utf-8'))
	print("Sent: ", msg)

# ================
# Realsense Setup
# ================

# Configure depth and color streams
pipeline = rs.pipeline()
config = rs.config()

# Get device product line for setting a supporting resolution
pipeline_wrapper = rs.pipeline_wrapper(pipeline)
pipeline_profile = config.resolve(pipeline_wrapper)
device = pipeline_profile.get_device()
device_product_line = str(device.get_info(rs.camera_info.product_line))

found_rgb = False
for s in device.sensors:
	if s.get_info(rs.camera_info.name) == 'RGB Camera':
		found_rgb = True
		break
if not found_rgb:
	print("The demo requires Depth camera with Color sensor")
	exit(0)

config.enable_stream(rs.stream.depth, 640, 480, rs.format.z16, 30)
config.enable_stream(rs.stream.color, 640, 480, rs.format.bgr8, 30)

# ArUco
arucoDict = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_6X6_250)
arucoParams = cv2.aruco.DetectorParameters()
arucoDetector = cv2.aruco.ArucoDetector(arucoDict, arucoParams)

# Start streaming
pipeline.start(config)

# ================
# Server loop
# ================

try:
	with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
		sock.connect((HOST, PORT))

		while True:
			# try:
				# ==== FRAME QUERYING ====
				frames = pipeline.wait_for_frames()
				depth_frame = frames.get_depth_frame()
				color_frame = frames.get_color_frame()
				if not depth_frame or not color_frame:
					continue

				color_image = np.asanyarray(color_frame.get_data())

				corners, ids, rejected = arucoDetector.detectMarkers(color_image)

				# ==== MARKER TRACKING ====
				depthIntrinsics = depth_frame.profile.as_video_stream_profile().intrinsics
				
				for i, cornerSet in enumerate(corners):
					assert(cornerSet.shape[0] == 1)
					cornerSet = cornerSet[0, ...]

					(cornerA_x, cornerA_y) = cornerSet[0]
					(cornerB_x, cornerB_y) = cornerSet[2]

					centerSS = [(cornerA_x + cornerB_x) / 2.0, (cornerA_y + cornerB_y) / 2]
					centerZ = depth_frame.get_distance(centerSS[0], centerSS[1])

					centerWS = rs.rs2_deproject_pixel_to_point(depthIntrinsics, centerSS, centerZ)
					
					id = ids[i][0]
					MarkerCentroids[id] = centerWS
					if MarkerAges[id] != -2:
						MarkerAges[id] = CurrentTime

				# ==== SERVER MESSAGES ====
				if HasCalibrated == False:
					msg = receive(sock)

					# Really shouldn't need this line
					if (len(msg) == 0):
						continue

					incomingIds = msg["OutgoingIds"]
					incomingPositions = msg["OutgoingPositions"]

					incomingCount = len(incomingIds)

					inPointList = np.zeros((incomingCount, 4))
					outPointList = np.zeros((incomingCount, 4))
					for i, incomingId in enumerate(incomingIds):
						id = incomingId
						pos = incomingPositions[i]

						MarkerAges[id] = -2 # Lock the lifetime to indicate use in calibration

						inPointList[i] = np.append(MarkerCentroids[id], 1.0)
						outPointList[i] = [pos['x'], pos['y'], pos['z'], 1.0]

					CalibrationMatrix, residuals, rank, s = np.linalg.lstsq(inPointList, outPointList, rcond = None)
					
					outMatrix = CalibrationMatrix.tolist()
					with open("CalibrationMatrix.json", 'w') as json_file:
						json.dump(outMatrix, json_file)

					HasCalibrated = True
				else:
					outMarkerIds = []
					outMarkerCentroids = []
					for i, markerAge in enumerate(MarkerAges):
						# Ignore calibrants and unencountereds
						if markerAge < 0:
							continue

						outId = i
						outMarkerCentroid = {"x": -999.0, "y": -999.0, "z": -999.0}
						if (CurrentTime - markerAge) > LIFETIME_THRESHOLD:
							outMarkerCentroid = {"x": -999.0, "y": -999.0, "z": -999.0}
						else:
							centroid = MarkerCentroids[i]
							centroid = CalibrationMatrix.transpose().dot(np.append(centroid, 1.0))
							outMarkerCentroid = {"x": centroid[0].item(), "y": centroid[1].item(), "z": centroid[2].item()}
						
						outMarkerIds.append(outId)
						outMarkerCentroids.append(outMarkerCentroid)

					msg["IncomingIds"] = outMarkerIds
					msg["IncomingPositions"] = outMarkerCentroids
					send(sock, msg)

				CurrentTime += 1
			# except KeyboardInterrupt:
			# 	exit()
finally:
	# Stop streaming
	pipeline.stop()