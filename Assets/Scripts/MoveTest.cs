using Unity.Netcode;
using UnityEngine;

public class MoveTest : NetworkBehaviour
{
    [SerializeField] private Transform _spawnedObjectPrefab;

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;

        Vector3 moveDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) { moveDir.z = +1f; }
        if (Input.GetKey(KeyCode.S)) { moveDir.z = -1f; }
        if (Input.GetKey(KeyCode.A)) { moveDir.x = -1f; }
        if (Input.GetKey(KeyCode.D)) { moveDir.x = +1f; }
        if (Input.GetKey(KeyCode.Space)) { 
           Transform spawnedObject = Instantiate(_spawnedObjectPrefab);
            spawnedObject.GetComponent<NetworkObject>().Spawn(true);
        }

        float moveSpeed = 10f;
        transform.position += moveSpeed * Time.deltaTime * moveDir;
    }
}