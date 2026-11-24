namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>Small, realistic import fixtures. <c>@@BASE@@</c> stands for the loopback server URL where a request is sent.</summary>
internal static class Milestone5Samples
{
    public const string PostmanCollection = """
        {
          "info": {
            "name": "Shop API",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
          },
          "auth": { "type": "bearer", "bearer": [ { "key": "token", "value": "{{token}}", "type": "string" } ] },
          "variable": [ { "key": "baseUrl", "value": "@@BASE@@" } ],
          "item": [
            {
              "name": "Products",
              "item": [
                {
                  "name": "List products",
                  "event": [ { "listen": "prerequest", "script": { "type": "text/javascript", "exec": [ "console.log('pre-list');" ] } } ],
                  "request": {
                    "method": "GET",
                    "url": {
                      "raw": "{{baseUrl}}/echo?page=1&limit=10",
                      "host": [ "{{baseUrl}}" ],
                      "path": [ "echo" ],
                      "query": [ { "key": "page", "value": "1" }, { "key": "limit", "value": "10" } ]
                    }
                  }
                },
                {
                  "name": "Create product",
                  "event": [ { "listen": "test", "script": { "type": "text/javascript", "exec": [ "pm.environment.set('lastStatus', String(pm.response.code));" ] } } ],
                  "request": {
                    "method": "POST",
                    "header": [ { "key": "Content-Type", "value": "application/json" } ],
                    "body": { "mode": "raw", "raw": "{\"name\":\"Widget\"}", "options": { "raw": { "language": "json" } } },
                    "url": "{{baseUrl}}/echo"
                  }
                }
              ]
            },
            {
              "name": "Upload avatar",
              "request": {
                "method": "POST",
                "body": { "mode": "formdata", "formdata": [ { "key": "avatar", "type": "file", "src": "./avatar.png" }, { "key": "caption", "value": "me", "type": "text" } ] },
                "url": "{{baseUrl}}/echo"
              }
            }
          ]
        }
        """;

    public const string PostmanEnvironment = """
        {
          "id": "6c0d0a3e-0000-4000-8000-000000000001",
          "name": "Staging",
          "values": [
            { "key": "token", "value": "tok-123", "type": "default", "enabled": true },
            { "key": "apiSecret", "value": "s3cr3t-VALUE", "type": "secret", "enabled": true }
          ],
          "_postman_variable_scope": "environment"
        }
        """;

    public const string OpenApiYaml = """
        openapi: 3.0.3
        info:
          title: Pet Store
          version: 1.0.0
        servers:
          - url: https://pets.example.test/v1
        security:
          - bearerAuth: []
        tags:
          - name: pets
          - name: owners
        paths:
          /pets:
            get:
              tags: [pets]
              summary: List pets
              operationId: listPets
            post:
              tags: [pets]
              summary: Create pet
              requestBody:
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Pet'
          /owners/{ownerId}:
            get:
              tags: [owners]
              summary: Get owner
              parameters:
                - name: ownerId
                  in: path
                  required: true
                  schema:
                    type: string
        components:
          securitySchemes:
            bearerAuth:
              type: http
              scheme: bearer
          schemas:
            Pet:
              type: object
              properties:
                name:
                  type: string
        """;

    public const string Swagger2Json = """
        {
          "swagger": "2.0",
          "info": { "title": "Pet Store", "version": "1.0.0" },
          "host": "pets.example.test",
          "basePath": "/v1",
          "schemes": [ "https" ],
          "securityDefinitions": { "basicAuth": { "type": "basic" } },
          "security": [ { "basicAuth": [] } ],
          "paths": {
            "/pets": {
              "get": { "tags": [ "pets" ], "summary": "List pets", "operationId": "listPets" },
              "post": {
                "tags": [ "pets" ],
                "summary": "Create pet",
                "consumes": [ "application/json" ],
                "parameters": [ { "in": "body", "name": "body", "schema": { "$ref": "#/definitions/Pet" } } ]
              }
            },
            "/owners/{ownerId}": {
              "get": {
                "tags": [ "owners" ],
                "summary": "Get owner",
                "parameters": [ { "name": "ownerId", "in": "path", "required": true, "type": "string" } ]
              }
            }
          },
          "definitions": { "Pet": { "type": "object", "properties": { "name": { "type": "string" } } } }
        }
        """;

    public const string Curl = """
        curl '@@BASE@@/echo?x=1' \
          -X POST \
          -H 'accept: */*' \
          -H 'content-type: application/json' \
          -H 'x-trace: abc-123' \
          --data-raw '{"hello":"world"}'
        """;

    public const string Har = """
        {
          "log": {
            "version": "1.2",
            "entries": [
              {
                "_resourceType": "fetch",
                "request": { "method": "GET", "url": "https://api.example.test/users?active=true", "headers": [ { "name": "Accept", "value": "application/json" } ], "queryString": [ { "name": "active", "value": "true" } ] },
                "response": { "status": 200, "content": { "mimeType": "application/json" } }
              },
              {
                "_resourceType": "fetch",
                "request": { "method": "POST", "url": "https://api.example.test/users", "headers": [ { "name": "Content-Type", "value": "application/json" } ], "postData": { "mimeType": "application/json", "text": "{\"name\":\"Ada\"}" } },
                "response": { "status": 201, "content": { "mimeType": "application/json" } }
              },
              {
                "_resourceType": "image",
                "request": { "method": "GET", "url": "https://cdn.example.test/logo.png", "headers": [] },
                "response": { "status": 200, "content": { "mimeType": "image/png" } }
              },
              {
                "_resourceType": "fetch",
                "request": { "method": "OPTIONS", "url": "https://api.example.test/users", "headers": [ { "name": "Access-Control-Request-Method", "value": "POST" } ] },
                "response": { "status": 204, "content": { "mimeType": "text/plain" } }
              },
              {
                "_resourceType": "fetch",
                "request": { "method": "GET", "url": "https://api.example.test/users?active=true", "headers": [ { "name": "Accept", "value": "application/json" } ], "queryString": [ { "name": "active", "value": "true" } ] },
                "response": { "status": 200, "content": { "mimeType": "application/json" } }
              }
            ]
          }
        }
        """;

    public const string Insomnia = """
        {
          "_type": "export",
          "__export_format": 4,
          "resources": [
            { "_id": "wrk_1", "_type": "workspace", "name": "Billing", "description": "Billing API" },
            { "_id": "env_base", "_type": "environment", "parentId": "wrk_1", "name": "Base Environment", "data": { "baseUrl": "https://billing.example.test", "token": "base-token" } },
            { "_id": "env_stg", "_type": "environment", "parentId": "env_base", "name": "Staging", "data": { "baseUrl": "https://staging.billing.example.test" } },
            { "_id": "fld_1", "_type": "request_group", "parentId": "wrk_1", "name": "Invoices" },
            {
              "_id": "req_1", "_type": "request", "parentId": "fld_1", "name": "List invoices", "method": "GET",
              "url": "{{ _.baseUrl }}/invoices", "headers": [ { "name": "X-Request-Id", "value": "{% uuid 'v4' %}" } ],
              "authentication": { "type": "bearer", "token": "{{ _.token }}" }
            },
            {
              "_id": "req_2", "_type": "request", "parentId": "wrk_1", "name": "Create invoice", "method": "POST",
              "url": "{{ _.baseUrl }}/invoices", "headers": [ { "name": "X-Prev", "value": "{% response 'body', 'req_1', 'b64::JC5pZA==::46b', 'never', 60 %}" } ],
              "body": { "mimeType": "application/json", "text": "{\"amount\": 10}" }
            }
          ]
        }
        """;

    public const string BrunoJson = """{ "version": "1", "name": "Library", "type": "collection" }""";

    public const string BrunoFolder = """
        meta {
          name: Books
          seq: 1
        }
        """;

    public const string BrunoGet = """
        meta {
          name: Get book
          type: http
          seq: 1
        }

        get {
          url: https://library.example.test/books/1
          body: none
          auth: none
        }
        """;

    public const string BrunoPost = """
        meta {
          name: Add book
          type: http
          seq: 2
        }

        post {
          url: https://library.example.test/books
          body: json
          auth: none
        }

        body:json {
          {"title": "Dune"}
        }
        """;

    public const string BrunoEnvironment = """
        vars {
          baseUrl: https://library.example.test
        }
        vars:secret [
          apiToken
        ]
        """;

    /// <summary>An OpenAPI document whose references point to a URL and to a local file.</summary>
    public const string OpenApiMaliciousRefs = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Evil", "version": "1" },
          "paths": {
            "/a": {
              "post": {
                "summary": "Remote ref",
                "requestBody": { "$ref": "@@BASE@@/echo#/components/requestBodies/X" }
              }
            },
            "/b": {
              "post": {
                "summary": "File ref",
                "requestBody": { "$ref": "file:///C:/Windows/win.ini#/x" }
              }
            }
          }
        }
        """;
}
