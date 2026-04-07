using Unity.VisualScripting;
using UnityEditor.SearchService;
using UnityEngine;

public class RockUpright : MonoBehaviour
{
    GameObject obj;
    private bool hasHitPlayer = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        obj = gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "FirstPersonController" && !hasHitPlayer)
        {
            hasHitPlayer = true;
            collision.gameObject.GetComponent<PlayerManager>().hitPlayer();
        }

        if (collision.gameObject.layer == LayerMask.NameToLayer("Floor"))
        {
            obj.transform.rotation = Quaternion.identity;
            obj.transform.position = new Vector3(obj.transform.position.x, 1.43613f, obj.transform.position.z);
            GameManager.Instance.boulders.Add(obj);

            if (obj.GetComponent<Rigidbody>() != null) {
                Destroy(obj.GetComponent<Rigidbody>());
            }
            Destroy(obj.GetComponent<RockUpright>());
        }
    }
}
