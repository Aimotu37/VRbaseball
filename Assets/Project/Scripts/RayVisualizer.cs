using UnityEngine;
using UnityEngine.InputSystem;

public class RayVisualizer : MonoBehaviour
{
    void Update()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Debug.Log($"鼠标位置: {mousePosition}");  // 添加这行，看鼠标位置是否变化
        
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * 10, Color.red);

        if (Physics.Raycast(ray, out hit, 10))
        {
            Debug.Log($"🎯 击中: {hit.collider.name}");
        }
    }
}