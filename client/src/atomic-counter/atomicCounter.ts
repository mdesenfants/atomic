export interface ICounter {
    counterName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}

export async function increment(counter: string, key: string): Promise<void> {
    await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}/increment?key=${key}`, {
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json"
        },
        method: "POST"
    });
}

export async function count(counter: string, key: string): Promise<number> {
    return await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}/count?key=${key}`, {
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json"
        },
        method: "GET"
    }).then(v => v.json() as unknown as number);
}

export async function getAuthToken(token: string, provider: string): Promise<string> {
    const response = await fetch(`https://atomiccounter.azurewebsites.net/.auth/login/${provider}`, {
        body: JSON.stringify({
            id_token: token
        }),
        headers: {
            "Content-Type": "application/json"
        },
        method: "POST",
    });

    const ez = await response.json();

    return ez.authenticationToken as string;
}

export class AtomicCounterClient {
    private token: string;

    constructor(authToken: string) {
        this.token = authToken;
    }

    public async createCounter(counter: string) {
        return await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${encodeURI(counter)}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "POST"
        }).then(t => t.json() as unknown as ICounter);
    }

    public async getCounter(counter: string) {
        return await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${encodeURI(counter)}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "GET",
        }).then(t => t.json() as unknown as ICounter);
    }

    public async increment(counter: string): Promise<void> {
        const meta = await this.getCounter(counter);
        const key = meta.writeKeys[0];
        await increment(counter, key);
    }

    public async count(counter: string): Promise<number> {
        const meta = await this.getCounter(counter);
        const key = meta.readKeys[0];
        return await count(counter, key).catch(() => 0);
    }

    public async reset(counter: string): Promise<void> {
        await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": this.token
            },
            method: "DELETE",
        });
    }
}