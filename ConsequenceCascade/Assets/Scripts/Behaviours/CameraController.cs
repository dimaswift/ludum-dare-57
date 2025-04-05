using UnityEngine;

namespace ConsequenceCascade.Behaviours
{
    public class CameraController : MonoBehaviour
    {
        public float damping = 1f;
        public float movementSpeed = 10f;

        public float fastMovementSpeed = 100f;

        public float freeLookSensitivity = 3f;
    
        public float zoomSensitivity = 10f;

        public float fastZoomSensitivity = 50f;

    
        private bool looking;
        private Transform camTransform;
        private Camera cam;

        private Vector3 clickPoint;
        private float targetSize;
        private bool dragging;
        private Vector3 zoomPoint;
        
        private void Awake()
        {
            cam = Camera.main;
            camTransform = transform;
            targetSize = cam.orthographicSize;
        }

        public void SetSize(float size)
        {
            cam.orthographicSize = size;
            targetSize = size;
        }

        private void ProcessPerspective()
        {
            var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var speed = fastMode ? fastMovementSpeed : movementSpeed;
            speed /= damping;
            var delta = speed * Time.deltaTime;
        
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                camTransform.position = transform.position + (-camTransform.right * delta);
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                camTransform.position = transform.position + (camTransform.right * delta);
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                camTransform.position = transform.position + (camTransform.forward * delta);
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                camTransform.position = transform.position + (-camTransform.forward * delta);
            }
            
            if (Input.GetKeyDown(KeyCode.Mouse1))
            {
                StartLooking();
            }
            else if (Input.GetKeyUp(KeyCode.Mouse1))
            {
                StopLooking();
            }
            
            if (looking)
            {
                var euler = camTransform.eulerAngles;
                var newRotationX = euler.y + Input.GetAxis("Mouse X") * freeLookSensitivity;
                var newRotationY = euler.x - Input.GetAxis("Mouse Y") * freeLookSensitivity;
                camTransform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
            }

            var axis = Input.mouseScrollDelta.y;
            
            if (axis != 0)
            {
                var zoom = fastMode ? fastZoomSensitivity : zoomSensitivity;
                camTransform.position = transform.position + camTransform.forward * (axis * zoom);
            }
        }
        
        private void ProcessOrthographic()
        {
            if (Input.GetMouseButtonDown(2))
            {
                clickPoint = cam.ScreenToWorldPoint(Input.mousePosition);
                dragging = true;
            }
            
            if (Input.GetMouseButtonUp(2))
            {
                dragging = false;
            }
            
            if (dragging)
            {
                camTransform.position = transform.position + (clickPoint - cam.ScreenToWorldPoint(Input.mousePosition));
            }
     
            var axis = Input.mouseScrollDelta.y;
            
            if (axis != 0)
            {
                targetSize -= (axis * zoomSensitivity * 0.1f);
                targetSize = Mathf.Max(0.1f, targetSize);
            }
            var mouseOffsetBefore = cam.ScreenToWorldPoint(Input.mousePosition);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * 10f);
            var mouseOffsetAfter = cam.ScreenToWorldPoint(Input.mousePosition);
            camTransform.position = transform.position + (mouseOffsetBefore - mouseOffsetAfter);
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }

            if (cam.orthographic)
            {
                ProcessOrthographic();
            }
            else
            {
                ProcessPerspective();
            }

        }

        private void OnDisable()
        {
            StopLooking();
        }

        private void StartLooking()
        {
            looking = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void StopLooking()
        {
            looking = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}