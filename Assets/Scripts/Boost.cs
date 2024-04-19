using UnityEngine;

public class Boost : MonoBehaviour
{
    public Rigidbody carRigidbody;
    [Header("Boost Settings")]
    [SerializeField] private float boostForce = 1000f;
    [SerializeField] private ParticleSystem boostParticles;

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        if (carRigidbody == null)
        {
            Debug.LogError("Rigidbody component not found on " + gameObject.name);
        }
    }

    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            boostParticles.Play();
            carRigidbody.AddForce(transform.forward * boostForce, ForceMode.Force);
        } else {
            boostParticles.Stop();
        }
    }
}
