using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ColocarCubo : MonoBehaviour
{
    [SerializeField] private GameObject cuboPrefab;

    private ARRaycastManager raycastManager;
    private static readonly List<ARRaycastHit> impactos = new List<ARRaycastHit>();
    private GameObject cuboColocado;

    private void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
    }

    private void Update()
    {
        var pantalla = Touchscreen.current;
        if (pantalla == null) return;

        var toque = pantalla.primaryTouch;
        if (!toque.press.wasPressedThisFrame) return;

        Vector2 posicion = toque.position.ReadValue();

        if (raycastManager.Raycast(posicion, impactos, TrackableType.PlaneWithinPolygon))
        {
            Pose pose = impactos[0].pose;
            // El pivote del cubo está en su centro; lo subimos media altura para que apoye en el piso.
            Vector3 elevacion = Vector3.up * (cuboPrefab.transform.localScale.y * 0.5f);

            if (cuboColocado == null)
                cuboColocado = Instantiate(cuboPrefab, pose.position + elevacion, pose.rotation);
            else
                cuboColocado.transform.SetPositionAndRotation(pose.position + elevacion, pose.rotation);
        }
    }
}