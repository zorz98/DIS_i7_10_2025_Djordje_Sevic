{{/*
Common labels applied to every resource in this chart.
*/}}
{{- define "greenfinance.labels" -}}
app.kubernetes.io/part-of: greenfinance
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
{{- end }}

{{/*
Full image reference for a given service name, e.g. "reference-data-service".
*/}}
{{- define "greenfinance.image" -}}
{{ .root.Values.image.registry }}/{{ .root.Values.image.repository }}/{{ .name }}:{{ .root.Values.image.tag }}
{{- end }}
