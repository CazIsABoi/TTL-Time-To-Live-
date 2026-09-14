using UnityEngine;
using UnityEngine.InputSystem;

public class Controller : MonoBehaviour
{
    [SerializeField] private InputActionReference showHideGrid;

    private void Start()
    {
        if (showHideGrid != null)
            showHideGrid.action.started += ShowHideGrid;
    }

    private void ShowHideGrid(InputAction.CallbackContext context)
    {
        print("Grid Toggled");
    }
}
