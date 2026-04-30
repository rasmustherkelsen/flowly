# Encapsulation Conventions

- Keep types internal unless they are to be used by the consumer.

- If types are needed by sibling assemblies like Flowly.RabbitMQ needs the Flowly assembly for the Transport Abstractions put types into nested namespaces so as to not pollute the root namespace.

