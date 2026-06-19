# Kubernetes deployment

Apply the manifests in this order:

```bash
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -f deploy/k8s/configmap.yaml
kubectl apply -f deploy/k8s/secret.example.yaml
kubectl apply -f deploy/k8s/web-deployment.yaml
kubectl apply -f deploy/k8s/api-deployment.yaml
kubectl apply -f deploy/k8s/services.yaml
kubectl apply -f deploy/k8s/ingress.yaml
```

For production, replace `secret.example.yaml` with a real `Secret`, or install External Secrets Operator and adapt `external-secret.example.yaml` to the cloud secret manager in use.

The app also supports mounted key-per-file secrets. When using CSI Secret Store or another mount-based provider, set:

```yaml
Secrets__KeyPerFile__Enabled: "true"
Secrets__KeyPerFile__Path: "/mnt/secrets-store"
```

Secret file names should use double underscores, for example `ConnectionStrings__DefaultConnection`.
