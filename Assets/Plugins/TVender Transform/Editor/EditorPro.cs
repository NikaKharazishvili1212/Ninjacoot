//#define VTRANSFORM_PRO
#if VTRANSFORM_PRO

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TVender.VTransform
{
    [CustomEditor(typeof(Transform), true), CanEditMultipleObjects]
    public class EditorPro : Editor
    {
        private TriangleMesh triangleMesh;
        public static float rad = 0;
        Vector3 offsetT = Vector3.zero;

        private void OnEnable()
        {
            //triangleMesh = new TriangleMesh();
        }

        //[DrawGizmo(GizmoType.Selected)]
        //static void OnDrawGizmosSelected(Transform transform, GizmoType gizmoType)
        //{           //return;
        //DrawTriangular(transform);
        //}

        protected virtual void OnSceneGUI()
        {
            //HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            /*
			var transform = (Transform)target;
			var to = transform.position + transform.rotation * Vector3.forward;

			var currentEvent = Event.current;
			if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && !currentEvent.alt)
			{
				Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
				RaycastHit hit;

				if (Physics.Raycast(ray, out hit, 100.0f))
				{
					Debug.Log(hit.barycentricCoordinate);

					//offsetT = hit.barycentricCoordinate;
					//Selection.SetActiveObjectWithContext(hit.transform.gameObject, null);

					var collider = hit.collider as MeshCollider;
					if (collider == null)
						return;

					var mesh = ((MeshCollider)collider).sharedMesh;

					triangleMesh.SetVertices(mesh.vertices[mesh.triangles[hit.triangleIndex * 3 + 0]],
						mesh.vertices[mesh.triangles[hit.triangleIndex * 3 + 1]],
						mesh.vertices[mesh.triangles[hit.triangleIndex * 3 + 2]]);
					triangleMesh.SetNormals(mesh.normals[mesh.triangles[hit.triangleIndex * 3 + 0]],
						mesh.normals[mesh.triangles[hit.triangleIndex * 3 + 1]],
						mesh.normals[mesh.triangles[hit.triangleIndex * 3 + 2]]);

					//var json = JsonUtility.ToJson(tm);
					//triangleMesh.stringValue = json;
					//serializedObject.Update();
				}
			}

			DrawTriangular(transform);

			if (Handles.Button(to + offsetT, Quaternion.identity, 1f, 1f, TexHandleCap))
			{
				//Undo.RecordObject(transform, "Snapped To Ground");
				//transform.localScale *= 0.5f;
			}
			*/
        }

        private void DrawTriangular(Transform transform)
        {
            //(transform as Editor).serializedObject.FindProperty("TriangleMesh");

            Gizmos.color = Color.blue;
            var position = (triangleMesh.Vertices[0] + triangleMesh.Vertices[1] + triangleMesh.Vertices[2]) / 3;
            //Gizmos.DrawMesh(triangleMesh.GetMesh(), transform.position, Quaternion.identity, Vector3.one);
            var p0 = transform.TransformPoint(triangleMesh.Vertices[0]);
            var p1 = transform.TransformPoint(triangleMesh.Vertices[1]);
            var p2 = transform.TransformPoint(triangleMesh.Vertices[2]);

            Handles.DrawLine(p0, p1);
            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p0, p2);
        }

        private void TexHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            var dis = HandleUtility.DistanceToRectangle(position, rotation, size * 0.2f);

            switch (eventType)
            {
                case EventType.MouseMove:
                case EventType.Layout:
                    {
                        HandleUtility.AddControl(controlID, dis);
                        break;
                    }
                case EventType.Repaint:
                    {
                        if (dis == 0)
                        {
                            Handles.Label(position, GUI.GUILeftMiddle, Styles.Button64);
                        }
                        else
                        {
                            Handles.Label(position, GUI.GUILeftMiddle, Styles.Button32);
                        }

                        break;
                    }
            }
        }
    }
}

#endif