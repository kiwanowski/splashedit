using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PSXSplash.Runtime
{
  public class PSXSceneExporter : MonoBehaviour
  {
    public void Export()
    {
      Debug.Log($"Exporting scene: {SceneManager.GetActiveScene().name}");

      Scene activeScene = SceneManager.GetActiveScene();
      foreach (GameObject obj in activeScene.GetRootGameObjects())
      {
        ExportAllPSXExporters(obj.transform);
      }
    }

    void ExportAllPSXExporters(Transform parentTransform)
    {
      PSXObjectExporter exporter = parentTransform.GetComponent<PSXObjectExporter>();

      if (exporter != null)
      {
        exporter.Export();
      }

      foreach (Transform child in parentTransform)
      {
        ExportAllPSXExporters(child);
      }
    }

    void OnDrawGizmos()
    {
      Gizmos.DrawIcon(transform.position, "Packages/net.psxsplash.splashedit/Icons/PSXSceneExporter.png", true);
    }
  }
}
