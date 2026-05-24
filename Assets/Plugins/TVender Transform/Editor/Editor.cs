#define USE_TVENDER_TRANSFORM
#if USE_TVENDER_TRANSFORM

using System.Reflection;
using System;
using UnityEngine;
using UnityEditor;

namespace TVender.VTransform
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Transform))]
	public partial class Editor : UnityEditor.Editor
	{
		static SnapOptions _snapOption = SnapOptions.Auto;

		const float _ICON_SIZE = 20f;

		const float _FLOAT_BUTTON_WIDTH = 40f;

		const float _MAX_POSITION = 100000f;

		static Type _inspectorType;

		UnityEditor.Editor EDITOR_INSTANCE;

		string helpMessage = "";

		static Editor()
		{
			var assembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
			_inspectorType = assembly.GetType("UnityEditor.TransformInspector", false);
		}

		public void OnEnable()
		{
			if (EDITOR_INSTANCE == null)
				EDITOR_INSTANCE = CreateEditor(targets, _inspectorType);
		}

		void OnDisable()
		{
			if (EDITOR_INSTANCE)
				DestroyImmediate(EDITOR_INSTANCE);
		}

		public override void OnInspectorGUI()
		{
			#if DEBUG_LOG
			Debug.Log("debug log");
			#endif

			if (target == null || targets.Length == 0)
				return;

			//use wide mode			
			if (!EditorGUIUtility.wideMode)
			{
				EditorGUIUtility.wideMode = true;
				EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth - 212;
			}

			var transform = (Transform)target;
			var y = EditorGUILayout.GetControlRect(GUILayout.Height(0), GUILayout.ExpandHeight(false)).y + 2;
			var x = GUILayoutUtility.GetLastRect().x + GUILayoutUtility.GetLastRect().width - _FLOAT_BUTTON_WIDTH + 2;
			var rect = new Rect(x, y, _ICON_SIZE, _ICON_SIZE);

			using (new EditorGUILayout.HorizontalScope())
			{
				using (new EditorGUILayout.VerticalScope())
				{
					//position reset
					if (UnityEngine.GUI.Button(rect, GUI.GUIResetPosition, Styles.Button16))
						SetPosition(Vector3.zero);

					//rotation rest
					using (new EditorGUI.DisabledGroupScope(transform.localRotation == Quaternion.identity))
					{
						rect.y += _ICON_SIZE;
						if (UnityEngine.GUI.Button(rect, GUI.GUIResetRotation, Styles.Button16))
							SetRotation(Vector3.zero);
					}

					//random rotation
					rect.x += _ICON_SIZE;
					if (UnityEngine.GUI.Button(rect, GUI.GUIRandom, Styles.Button16))
						SetRotation(UnityEngine.Random.rotation.eulerAngles);

					//reset scale
					rect.x -= _ICON_SIZE;
					rect.y += _ICON_SIZE;
					using (new EditorGUI.DisabledGroupScope(transform.localScale == Vector3.one))
					{
						if (UnityEngine.GUI.Button(rect, GUI.GUIResetScale, Styles.Button16))
							SetScale(Vector3.one);
					}

					EDITOR_INSTANCE.OnInspectorGUI();
				}
				GUILayout.Space(_FLOAT_BUTTON_WIDTH);
			}

			#region snap button
			using (new EditorGUILayout.HorizontalScope())
			{
				using (var check = new EditorGUI.ChangeCheckScope())
				{
					var s = (SnapOptions)EditorGUILayout.EnumPopup("Snap Down", _snapOption);
					if (check.changed)
					{
						_snapOption = s;
						ValidateSnap();
					}
				}

				using (new EditorGUI.DisabledGroupScope(!string.IsNullOrEmpty(helpMessage)))
				{
					if (GUILayout.Button(GUI.GUISnapDown, Styles.Button20))
					{
						SnapDown(transform);
					}
				}

				if (GUILayout.Button(GUI.GUISnapHelp, Styles.Button20))
				{
					//SnapDown(transform);
				}
			}

			using (new EditorGUILayout.VerticalScope())
			{
				EditorGUILayout.Space();
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (!string.IsNullOrEmpty(helpMessage))
				{
					EditorGUILayout.HelpBox(helpMessage, MessageType.Warning);
				}
			}
			#endregion

			#region world position
			using (new EditorGUILayout.VerticalScope())
				EditorGUILayout.Space();

			using (new EditorGUILayout.HorizontalScope())
			{
				var msg = $"Global position {{{transform.position.x},{transform.position.y},{transform.position.z}}}";
				EditorGUILayout.HelpBox(msg, MessageType.Info);
			}
			#endregion

			EditorGUIUtility.labelWidth = 0;
			ValidateSnap();
		}

		private void SnapDown(Transform transform)
		{
			var layer = transform.gameObject.layer;
			transform.gameObject.layer = 2;

			RaycastHit hit;
			if (Physics.Raycast(transform.position, Vector3.down, out hit))
			{
				//Debug.Log($"{hit.collider.name} {hit.point}");
				var offsetY = 0f;

				switch (_snapOption)
				{
					case SnapOptions.Auto:
						{
							var m = Mathf.Min(GetTriangleCenterPosition(transform), GetVertexPosition(transform));
							offsetY = Mathf.Min(m, GetColliderPosition(transform));
							break;
						}
					case SnapOptions.TriangleCenter:
						{
							offsetY = GetTriangleCenterPosition(transform);
							break;
						}
					case SnapOptions.Center:
						{
							offsetY = transform.TransformPoint(Vector3.zero).y;
							break;
						}
					case SnapOptions.Vertex:
						{
							offsetY = GetVertexPosition(transform);
							break;
						}
					case SnapOptions.Collider:
						{
							offsetY = GetColliderPosition(transform);
							break;
						}
				}

				Undo.RecordObject(transform, "Snapped To Ground");
				transform.position -= new Vector3(0, offsetY - hit.point.y, 0);
			}

			transform.gameObject.layer = layer;
		}

		public float GetTriangleCenterPosition(Transform transform)
		{
			var offset = _MAX_POSITION;

			//Debug.Log(transform.name);

			var meshFilter = transform.GetComponent<MeshFilter>();

			if (meshFilter != null)
			{
				var mesh = meshFilter.sharedMesh;
				//Debug.Log(mesh.vertices.Length);
				//Debug.Log(mesh.triangles.Length);

				for (var i = 0; i < mesh.triangles.Length; i += 3)
				{
					var v0 = mesh.vertices[mesh.triangles[i]];
					var v1 = mesh.vertices[mesh.triangles[i + 1]];
					var v2 = mesh.vertices[mesh.triangles[i + 2]];

					var center = (v0 + v1 + v2) / 3f;
					center = transform.TransformPoint(center);
					if (center.y < offset)
						offset = center.y;
				}
			}

			//Debug.Log($"triangle {offset}");
			return offset;
		}

		private float GetVertexPosition(Transform transform)
		{
			var offset = _MAX_POSITION;

			var meshFilter = transform.GetComponent<MeshFilter>();
			if (meshFilter != null)
			{
				var mesh = meshFilter.sharedMesh;
				foreach (var v3 in mesh.vertices)
				{
					var v = transform.TransformPoint(v3);
					if (v.y < offset)
						offset = v.y;
				}
			}

			//Debug.Log($"vertex {offset}");
			return offset;
		}

		private void SetPosition(Vector3 local)
		{
			if (Selection.transforms.Length > 1)
				Undo.RecordObjects(Selection.transforms, "change position");

			foreach (var transform in Selection.transforms)
			{
				if (Selection.transforms.Length == 1)
					Undo.RecordObject(transform, "change position");

				transform.localPosition = local;
			}
		}

		private void SetRotation(Vector3 eulerAngles)
		{
			if (Selection.transforms.Length > 1)
				Undo.RecordObjects(Selection.transforms, "change rotation");

			foreach (var transform in Selection.transforms)
			{
				if (Selection.transforms.Length == 1)
					Undo.RecordObject(transform, "change rotation");

				transform.localEulerAngles = eulerAngles;
			}
		}

		private void SetScale(Vector3 localScale)
		{
			if (Selection.transforms.Length > 1)
				Undo.RecordObjects(Selection.transforms, "change scale");

			foreach (var transform in Selection.transforms)
			{
				if (Selection.transforms.Length == 1)
					Undo.RecordObject(transform, "change scale");

				transform.localScale = localScale;
			}
		}

		private void ValidateSnap()
		{
			if (Selection.transforms.Length > 1)
			{
				helpMessage = "This version does not support multi-object snap.";
				return;
			}

			var transform = (Transform)target;
			var meshFilter = transform.GetComponent<MeshFilter>();

			helpMessage = "";
			switch (_snapOption)
			{
				case SnapOptions.TriangleCenter:
				case SnapOptions.Vertex:
					{
						if (meshFilter == null)
						{
							helpMessage = "Mesh filter is required for this mode.";
						}

						break;
					}
				case SnapOptions.Center:
					{
						break;
					}
				case SnapOptions.Collider:
					{
						var collider = transform.GetComponent<Collider>();
						if (collider == null)
						{
							helpMessage = "A collider is required for this mode.";
						}
						break;
					}
			}
		}

		private float GetColliderPosition(Transform transform)
		{
			var collider = transform.GetComponent<Collider>();
			if (collider != null)
			{
				var height = 0f;
				var center = Vector3.zero;
				var typeName = collider.GetType().Name;

				if (typeName == "BoxCollider" || typeName == "CapsuleCollider")
				{
					height = collider.bounds.size.y / 2;
				}

				if (typeName == "BoxCollider" || typeName == "CapsuleCollider" || typeName == "SphereCollider" || typeName == "WheelCollider")
				{
					var fieldInfo = collider.GetType().GetProperty("center");
					if (fieldInfo != null)
						center = (Vector3)fieldInfo.GetValue(collider);
				}

				if (typeName == "SphereCollider" || typeName == "WheelCollider")
				{
					var f = collider.GetType().GetProperty("radius");
					if (f != null)
						height = (float)f.GetValue(collider);
				}

				if (typeName == "MeshCollider")
				{
					var offset = _MAX_POSITION;
					var mesh = ((MeshCollider)collider).sharedMesh;

					foreach (var v3 in mesh.vertices)
					{
						var v = transform.TransformPoint(v3);
						if (v.y < offset)
							offset = v.y;
					}

					return offset;
				}

				return transform.TransformPoint(center).y - height;
				//Debug.Log($"collider {offset}");
			}

			return 0;
		}
	}
}
#endif