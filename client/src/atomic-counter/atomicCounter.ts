export interface ICounter {
    counterName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}

export async function increment(counter: string, key: string): Promise<void> {
    if (!counterNameIsValid(counter)) {
        // tslint:disable-next-line:no-console
        console.warn("Counter name must be valid before inrementing. Returning without increment.");
        return await Promise.resolve();
    }

    if (!key || key.trim() === "") {
        // tslint:disable-next-line:no-console
        console.warn("Must provide a write key. Returning without increment.");
        return await Promise.resolve();
    }
    
    await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}/increment?key=${key}`, {
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json"
        },
        method: "POST"
    });
}

export async function count(counter: string, key: string): Promise<number> {
    if (!counterNameIsValid(counter)) {
        // tslint:disable-next-line:no-console
        console.warn("Counter name must be valid before counting. Returning 0.");
        return await Promise.resolve(0);
    }

    if (!key || key.trim() === "") {
        // tslint:disable-next-line:no-console
        console.warn("Must provide a read key. Returning 0.");
        return await Promise.resolve(0);
    }

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

export function counterNameIsValid(input: string): boolean {
    return input.length > 3 && input.length < 54 && /[a-z0-9]+/.test(input)
}

export class AtomicCounterClient {
    private token: () => Promise<string>;

    constructor(authToken: () => Promise<string>) {
        this.token = authToken;
    }

    public async createCounter(counter: string) {
        if (!counterNameIsValid(counter)) {
            // tslint:disable-next-line:no-console
            console.warn("Counter name must be vaid before creation. Returning null.");
            return await Promise.resolve(null);
        }

        return await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${encodeURI(counter)}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": await this.token()
            },
            method: "POST"
        })
        .then(t => t.json() as unknown as ICounter)
        .catch(() => null);
    }

    public async getCounter(counter: string) {
        if (!counterNameIsValid(counter)) {
            // tslint:disable-next-line:no-console
            console.warn("Counter name must be valid before retrieving. Returning null.");
            return await Promise.resolve(null);
        }

        return await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${encodeURI(counter)}`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": await this.token()
            },
            method: "GET",
        }).then(t => t.json() as unknown as ICounter);
    }

    public async getCounters() {
        return await fetch('https://atomiccounter.azurewebsites.net/api/counters', {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": await this.token()
            },
            method: "GET",
        }).then(t => t.json() as unknown as string[]);
    }

    public async increment(counter: string): Promise<void> {
        if (!counterNameIsValid(counter)) {
            // tslint:disable-next-line:no-console
            console.warn("Counter name must be valid to increment. Returning early.");
            return await Promise.resolve();
        }

        const meta = await this.getCounter(counter);
        const key = meta ? meta.writeKeys[0] : null;
        
        if (!key) {
            // tslint:disable-next-line:no-console
            console.warn("No write keys found. Returning early without incrementing.");
            return Promise.resolve();
        }

        await increment(counter, key);
    }

    public async count(counter: string): Promise<number> {
        if (!counterNameIsValid(counter)) {
            // tslint:disable-next-line:no-console
            console.warn("Counter name must be valid to count. Returning 0.");
            return await Promise.resolve(0);
        }

        const meta = await this.getCounter(counter);
        const key = meta ? meta.readKeys[0] : null;
        
        if (!key) {
            // tslint:disable-next-line:no-console
            console.warn("Could not find read keys. Returning 0.");
            return await Promise.resolve(0);
        }

        return await count(counter, key).catch(() => 0);
    }

    public async reset(counter: string): Promise<void> {
        if (!counterNameIsValid(counter)) {
            // tslint:disable-next-line:no-console
            console.warn("Counter name must be valid before resetting. Returning without reset.");
            return await Promise.resolve();
        }
        
        await fetch(`https://atomiccounter.azurewebsites.net/api/counter/${counter}/reset`, {
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "X-ZUMO-AUTH": await this.token()
            },
            method: "POST",
        });
    }
}