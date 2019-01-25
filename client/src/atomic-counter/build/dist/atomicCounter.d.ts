export interface ICounter {
    counterName: string;
    origins: string[];
    writeKeys: string[];
    readKeys: string[];
}
export declare function increment(counter: string, key: string): Promise<void>;
export declare function count(counter: string, key: string): Promise<number>;
export declare function getAuthToken(token: string, provider: string): Promise<string>;
export declare class AtomicCounterClient {
    private token;
    constructor(authToken: () => Promise<string>);
    createCounter(counter: string): Promise<ICounter>;
    getCounter(counter: string): Promise<ICounter>;
    getCounters(): Promise<string[]>;
    increment(counter: string): Promise<void>;
    count(counter: string): Promise<number>;
    reset(counter: string): Promise<void>;
}
