Feature: JsonStream
	PostJsonStreamAsync serializes the JSON body straight into the request stream

Scenario: Post a JSON body as a stream
	When I post a JSON stream with Name "Customer A" and Count 3
	Then the captured request is a POST with Content-Type application/json; charset=utf-8
	And the captured request has no precomputed Content-Length
	And the captured body is {"name":"Customer A","count":3}
